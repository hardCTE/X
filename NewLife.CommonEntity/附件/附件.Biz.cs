﻿/*
 * XCoder v3.2.2010.1014
 * 作者：nnhy/NEWLIFE
 * 时间：2010-12-08 16:22:54
 * 版权：版权所有 (C) 新生命开发团队 2010
*/

using System;
using System.ComponentModel;
using System.IO;
using System.Web;
using System.Web.UI.WebControls;
using System.Xml.Serialization;
using NewLife.Configuration;
using XCode;

namespace NewLife.CommonEntity
{
    /// <summary>附件</summary>
    [ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
    public class Attachment : Attachment<Attachment, Statistics> { }

    /// <summary>附件</summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TStatistics"></typeparam>
    public class Attachment<TEntity, TStatistics> : Attachment<TEntity>
        where TEntity : Attachment<TEntity>, new()
        where TStatistics : Statistics<TStatistics>, new()
    {
        #region 统计
        private TStatistics _Stat;
        /// <summary>统计</summary>
        public TStatistics Stat
        {
            get
            {
                if (_Stat == null && StatID > 0 && !Dirtys.ContainsKey("Stat"))
                {
                    _Stat = Statistics<TStatistics>.FindByID(StatID);
                    Dirtys["Stat"] = true;
                }
                return _Stat;
            }
            set { _Stat = value; }
        }

        private static Object _incLock = new Object();
        /// <summary>增加统计</summary>
        /// <param name="remark"></param>
        public override void Increment(String remark = null)
        {
            if (Stat == null)
            {
                lock (_incLock)
                {
                    if (Stat == null)
                    {
                        var entity = new TStatistics();
                        entity.Save();

                        this.StatID = entity.ID;
                        this.Save();

                        if (Stat == null) Stat = entity;
                    }
                }
            }

            Stat.Increment(remark);
        }
        #endregion
    }

    /// <summary>附件</summary>
    /// <remarks>
    /// 对于文件的存放，可以考虑同一个文件只存放一份，方法就是通过名称、大小、散列三个同时比较
    /// </remarks>
    [BindIndex("IX_Attachment_Category", false, "Category")]
    public partial class Attachment<TEntity> : Entity<TEntity> where TEntity : Attachment<TEntity>, new()
    {
        #region 对象操作
        [NonSerialized]
        private HttpPostedFile _PostedFile;
        /// <summary>上传文件</summary>
        [XmlIgnore]
        public HttpPostedFile PostedFile { get { return _PostedFile; } set { _PostedFile = value; } }

        /// <summary>重载插入操作，在此保存附件，保存异常时自动回滚</summary>
        /// <returns></returns>
        protected override int OnInsert()
        {
            // 外部也可以提前调用GetFilePath
            if (String.IsNullOrEmpty(FilePath)) GetFilePath();

            var rs = base.OnInsert();

            var file = FullFilePath;
            if (PostedFile != null && !String.IsNullOrEmpty(file))
            {
                String path = Path.GetDirectoryName(file);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                // 如果文件已存在，也不保存。这个不大可能，只是为了保证万无一失
                if (!File.Exists(file)) PostedFile.SaveAs(file);

                PostedFile = null;
            }

            return rs;
        }

        /// <summary>已重载。</summary>
        /// <returns></returns>
        public override int Delete()
        {
            if (File.Exists(FullFilePath))
            {
                try
                {
                    File.Delete(FullFilePath);
                }
                catch { }
            }
            return base.Delete();
        }

        ///// <summary>验证数据。计算文件全路径</summary>
        ///// <param name="isNew"></param>
        //public override void Valid(bool isNew)
        //{
        //    base.Valid(isNew);
        //}
        #endregion

        #region 扩展属性
        ///// <summary>是否进行过查询</summary>
        //private static Boolean IsFindhttpHandler = false;
        //private static HttpHandlerAction _httpHandler;
        ///// <summary>Att httpHandler</summary>
        //public static HttpHandlerAction httpHandler
        //{
        //    get
        //    {
        //        if (!IsFindhttpHandler)
        //        {
        //            // 2011-11-25 大石头 我猜，IsFindhttpHandler应该是用来判断是否已经查找过了的吧
        //            IsFindhttpHandler = true;

        //            //foreach (HttpHandlerAction item in Config.GethttpHandlers())
        //            //{
        //            //    if (!String.IsNullOrEmpty(item.Type) && item.Type.IndexOf(typeof(AttachmentHttpHandler).FullName) > -1)
        //            //    {
        //            //        _httpHandler = item;

        //            //        break;
        //            //    }
        //            //}

        //            var type = AssemblyX.FindAllPlugins(typeof(AttachmentHttpHandler), true).FirstOrDefault();
        //            if (type != null) _httpHandler = type.CreateInstance();
        //        }
        //        return _httpHandler;
        //    }
        //}

        /////// <summary>获取Config中Handler设置</summary>
        ////[EditorBrowsable(EditorBrowsableState.Never)]
        ////[Obsolete("这个是不是拼写错误？使用HandlerUrl？")]
        ////public String HenderUrl { get { return HandlerUrl; } }

        ///// <summary>获取Config中Handler设置的用于访问当前附件的Url</summary>
        //public String HandlerUrl { get { return httpHandler == null ? null : String.Format("{0}?ID={1}", httpHandler.Path, ID); } }

        /// <summary>完全文件路径</summary>
        public String FullFilePath
        {
            get
            {
                if (String.IsNullOrEmpty(FilePath)) return null;
                return Path.Combine(GetConfigPath(Category), FilePath);
            }
        }

        private FileStream _FileStream;
        /// <summary>文件流</summary>
        public FileStream FileStream
        {
            get
            {
                //if (_FileStream == null && !Dirtys.ContainsKey("FileStream"))
                //{
                //    try
                //    {
                //        using (FileStream openFile = File.Open(FullFilePath, FileMode.Open, FileAccess.Read))
                //        {
                //            _FileStream = openFile;
                //        }
                //    }
                //    catch { };
                //    Dirtys.Add("FileStream", true);
                //}
                return _FileStream;
            }
            set { _FileStream = value; }
        }
        #endregion

        #region 扩展查询
        /// <summary>根据分类找附件</summary>
        /// <param name="catetory"></param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public static EntityList<TEntity> FindAllByCmdType(String catetory)
        {
            //return FindAll(__.Category, catetory) as EntityList<Attachment>;
            return FindAll(__.Category, catetory);
        }

        /// <summary>根据主键查询一个附件实体对象用于表单编辑</summary>
        /// <param name="id">编号</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByKeyForEdit(Int32 id)
        {
            TEntity entity = Find(new String[] { _.ID }, new Object[] { id });
            if (entity == null)
            {
                entity = new TEntity();
            }
            return entity;
        }

        /// <summary>根据编号查找</summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static TEntity FindByID(Int32 id)
        {
            //return Find(__.ID, id);
            // 实体缓存
            //return Meta.Cache.Entities.Find(__.ID, id);
            // 单对象缓存
            return Meta.SingleCache[id];
        }

        /// <summary>根据分类查找</summary>
        /// <param name="category">分类</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAllByCategory(String category)
        {
            if (Meta.Count >= 1000)
                return FindAll(new String[] { _.Category }, new Object[] { category });
            else // 实体缓存
                return Meta.Cache.Entities.FindAll(__.Category, category);
        }
        #endregion

        #region 高级查询
        // 以下为自定义高级查询的例子

        ///// <summary>
        ///// 查询满足条件的记录集，分页、排序
        ///// </summary>
        ///// <param name="key">关键字</param>
        ///// <param name="orderClause">排序，不带Order By</param>
        ///// <param name="startRowIndex">开始行，0表示第一行</param>
        ///// <param name="maximumRows">最大返回行数，0表示所有行</param>
        ///// <returns>实体集</returns>
        //[DataObjectMethod(DataObjectMethodType.Select, true)]
        //public static EntityList<Attachment> Search(String key, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        //{
        //    return FindAll(SearchWhere(key), orderClause, null, startRowIndex, maximumRows);
        //}

        ///// <summary>
        ///// 查询满足条件的记录总数，分页和排序无效，带参数是因为ObjectDataSource要求它跟Search统一
        ///// </summary>
        ///// <param name="key">关键字</param>
        ///// <param name="orderClause">排序，不带Order By</param>
        ///// <param name="startRowIndex">开始行，0表示第一行</param>
        ///// <param name="maximumRows">最大返回行数，0表示所有行</param>
        ///// <returns>记录数</returns>
        //public static Int32 SearchCount(String key, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        //{
        //    return FindCount(SearchWhere(key), null, null, 0, 0);
        //}

        /// <summary>构造搜索条件</summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        private static String SearchWhere(String key)
        {
            // WhereExpression重载&和|运算符，作为And和Or的替代
            var exp = SearchWhereByKeys(key);

            // 以下仅为演示，2、3行是同一个意思的不同写法，FieldItem重载了等于以外的运算符（第4行）
            //exp &= _.Name.Equal("testName")
            //    & !String.IsNullOrEmpty(key) & _.Name.Equal(key)
            //    .AndIf(!String.IsNullOrEmpty(key), _.Name.Equal(key))
            //    | _.ID > 0;

            return exp;
        }
        #endregion

        #region 扩展操作
        #endregion

        #region 扩展操作
        const String AttachmentPathKey = "NewLife.Attachment.Path";
        const String DefaultPath = @"..\Attachment\";

        /// <summary>根据类别获取相应的存放路径设置，如果不存在，则返回顶级设置路径后加上类别作为目录名</summary>
        /// <param name="category"></param>
        /// <returns></returns>
        static String GetConfigPath(String category)
        {
            String key = String.Empty;
            String config = String.Empty;

            if (String.IsNullOrEmpty(category))
            {
                key = AttachmentPathKey;
                config = Config.GetConfig<String>(key, DefaultPath);
            }
            else
            {
                key = String.Format("{0}_{1}", AttachmentPathKey, category);
                config = Config.GetConfig<String>(key);

                // 如果不存在，则返回顶级设置路径后加上类别作为目录名
                if (String.IsNullOrEmpty(config)) config = GetConfigPath(null).CombinePath(category);
            }

            // 加上当前目录
            config = config.GetFullPath();
            // 重新计算目录，去掉..等字符
            config = new DirectoryInfo(config).FullName;
            return config;
        }

        const String AttachmentFormatKey = "NewLife.Attachment.Format";
        const String DefaultFormat = @"yyyy\\MMdd";

        /// <summary>取得时间格式化的路径</summary>
        /// <returns></returns>
        static String GetFormatPath()
        {
            String format = Config.GetConfig<String>(AttachmentFormatKey, DefaultFormat);

            return String.Format("{0:" + format + "}", DateTime.Now);
        }
        #endregion

        #region 业务
        /// <summary>检查并设置文件存放名称，先尝试以原名存放，若有同名文件，则删除</summary>
        protected virtual void GetFilePath()
        {
            if (String.IsNullOrEmpty(FileName)) throw new ArgumentNullException("FileName");

            String root = GetConfigPath(Category);
            String path = Path.Combine(root, GetFormatPath());

            String file = FileName;
            Int32 n = 2;
            while (File.Exists(Path.Combine(path, file)))
            {
                file = String.Format("{0}_{1}{2}", Path.GetFileNameWithoutExtension(FileName), n++, Path.GetExtension(FileName));
            }

            file = Path.Combine(path, file);
            // 减去类别路径
            file = file.Substring(root.Length);
            if (file.StartsWith(@"\")) file = file.Substring(1);
            if (file.StartsWith(@"/")) file = file.Substring(1);
            FilePath = file;
        }

        /// <summary>增加统计</summary>
        /// <param name="remark"></param>
        public virtual void Increment(String remark = null) { }
        #endregion

        #region 上传
        /// <summary>图片分类</summary>
        public static readonly String Category_Image = "Image";

        /// <summary>文件分类</summary>
        public static readonly String Category_File = "File";

        /// <summary>保存上传文件</summary>
        /// <param name="fileUpload"></param>
        /// <param name="category"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public static TEntity SaveFile(FileUpload fileUpload, String category, String userName)
        {
            if (!fileUpload.HasFile) return null;

            return SaveFile(fileUpload.PostedFile, category, userName);
        }

        /// <summary>保存上传文件</summary>
        /// <param name="file"></param>
        /// <param name="category"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public static TEntity SaveFile(HttpPostedFile file, String category, String userName)
        {
            if (file == null) return null;

            // 内部保存文件，位于事务保护之后
            var att = Create(file);
            att.Category = category;
            att.UserName = userName;

            //att.GetFilePath();
            att.Save();

            return att;
        }

        /// <summary>保存上传文件</summary>
        /// <param name="fileUploads"></param>
        /// <param name="category"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public static EntityList<TEntity> SaveFile(HttpFileCollection fileUploads, String category, String userName)
        {
            EntityList<TEntity> atts = new EntityList<TEntity>();
            foreach (FileUpload item in fileUploads)
            {
                try
                {
                    TEntity att = SaveFile(item, category, userName);
                    if (att != null) atts.Add(att);
                }
                catch (Exception ex)
                {
                    // 原异常作为内部异常挂在新异常信息那里
                    throw new Exception(String.Format("“{0}”上传出错！[{1}]", item.FileName, ex.Message), ex);
                }
            }

            return atts.Count > 0 ? atts : null;
        }

        /// <summary>为上传文件创建附件实体对象。根据附件对象准备各种信息填充到附件对象中</summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static TEntity Create(HttpPostedFile file)
        {
            if (file == null || file.ContentLength <= 0) return null;

            var att = new TEntity();
            att.FileName = file.FileName;
            att.Size = file.ContentLength;// / 1024;
            att.Extension = Path.GetExtension(file.FileName);
            //att.Category = category;
            att.IsEnable = true;
            //att.FilePath = 
            att.ContentType = file.ContentType;
            att.UploadTime = DateTime.Now;
            //att.UserName = userName;
            //att.Save();
            //att.GetFilePath();

            // 这里必须赋值，在OnInsert阶段会保存附件
            att.PostedFile = file;

            return att;
        }

        //public Boolean SaveChecked(HttpFileCollection fileUploads)
        //{
        //    //1.是否开启上传
        //    //2.格式检查
        //    //3.文件大小检查
        //    return false;
        //}
        #endregion
    }

    partial interface IAttachment
    {
        /// <summary>完全文件路径</summary>
        String FullFilePath { get; }

        /// <summary>增加统计</summary>
        /// <param name="remark"></param>
        void Increment(String remark = null);
    }
}