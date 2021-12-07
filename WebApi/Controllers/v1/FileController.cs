﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Repository.Database;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using WebApi.Libraries;
using WebApi.Models.Shared;

namespace WebApi.Controllers.v1
{

    /// <summary>
    /// 文件上传控制器
    /// </summary>
    [ApiVersion("1")]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerCore
    {



        /// <summary>
        /// 远程单文件上传接口
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="fileInfo">Key为文件URL,Value为文件名称</param>
        /// <returns>文件ID</returns>
        [HttpPost("RemoteUploadFile")]
        public long RemoteUploadFile([FromQuery][Required] string business, [FromQuery][Required] long key, [FromQuery][Required] string sign, [Required][FromBody] DtoKeyValue fileInfo)
        {
            string remoteFileUrl = fileInfo.Key.ToString();

            var fileExtension = Path.GetExtension(fileInfo.Value.ToString()).ToLower();
            var fileName = Guid.NewGuid().ToString() + fileExtension;

            string basepath = "Files/" + DateTime.UtcNow.ToString("yyyy/MM/dd");

            var filePath = Libraries.IO.Path.ContentRootPath() + "/" + basepath + "/";

            //下载文件
            var dlPath = Common.IO.IOHelper.DownloadFile(remoteFileUrl, filePath, fileName);

            if (dlPath == null)
            {
                Thread.Sleep(5000);
                dlPath = Common.IO.IOHelper.DownloadFile(remoteFileUrl, filePath, fileName);
            }

            filePath = dlPath.Replace(Libraries.IO.Path.ContentRootPath(), "");


            if (dlPath != null)
            {
                var isSuccess = true;

                var upRemote = false;

                if (upRemote == true)
                {
                    var oss = new Common.AliYun.OssHelper();

                    var upload = oss.FileUpload(dlPath, basepath, fileInfo.Value.ToString());

                    if (upload)
                    {
                        Common.IO.IOHelper.DeleteFile(dlPath);
                    }
                    else
                    {
                        isSuccess = false;
                    }
                }

                if (isSuccess)
                {

                    TFile f = new();
                    f.Id = snowflakeHelper.GetId();
                    f.Name = fileInfo.Value.ToString();
                    f.Path = filePath;
                    f.Table = business;
                    f.TableId = key;
                    f.Sign = sign;
                    f.CreateUserId = userId;
                    f.CreateTime = DateTime.UtcNow;
                    db.TFile.Add(f);
                    db.SaveChanges();

                    return f.Id;
                }

            }

            HttpContext.Response.StatusCode = 400;
            HttpContext.Items.Add("errMsg", "文件上传失败");

            return default;
        }



        /// <summary>
        /// 单文件上传接口
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        [DisableRequestSizeLimit]
        [HttpPost("UploadFile")]
        public long UploadFile([FromQuery][Required] string business, [FromQuery][Required] long key, [FromQuery][Required] string sign, [Required] IFormFile file)
        {

            string basepath = "/Files/" + DateTime.UtcNow.ToString("yyyy/MM/dd");
            string filepath = Libraries.IO.Path.ContentRootPath() + basepath;

            Directory.CreateDirectory(filepath);

            var fileName = snowflakeHelper.GetId();
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fullFileName = string.Format("{0}{1}", fileName, fileExtension);

            string path = "";

            var isSuccess = false;

            if (file != null && file.Length > 0)
            {
                path = filepath + "/" + fullFileName;

                using (FileStream fs = System.IO.File.Create(path))
                {
                    file.CopyTo(fs);
                    fs.Flush();
                }

                var upRemote = false;

                if (upRemote)
                {

                    var oss = new Common.AliYun.OssHelper();

                    var upload = oss.FileUpload(path, "Files/" + DateTime.UtcNow.ToString("yyyy/MM/dd"), file.FileName);

                    if (upload)
                    {
                        Common.IO.IOHelper.DeleteFile(path);

                        path = "/Files/" + DateTime.UtcNow.ToString("yyyy/MM/dd") + "/" + fullFileName;
                        isSuccess = true;
                    }
                }
                else
                {
                    path = basepath + "/" + fullFileName;
                    isSuccess = true;
                }

            }

            if (isSuccess)
            {

                TFile f = new();
                f.Id = fileName;
                f.IsDelete = false;
                f.Name = file.FileName;
                f.Path = path;
                f.Table = business;
                f.TableId = key;
                f.Sign = sign;
                f.CreateUserId = userId;
                f.CreateTime = DateTime.UtcNow;
                db.TFile.Add(f);
                db.SaveChanges();

                return fileName;
            }
            else
            {
                HttpContext.Response.StatusCode = 400;

                HttpContext.Items.Add("errMsg", "文件上传失败");

                return default;
            }
        }



        /// <summary>
        /// 多文件上传接口
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">标记</param>
        /// <returns></returns>
        /// <remarks>swagger 暂不支持多文件接口测试，请使用 postman</remarks>
        [DisableRequestSizeLimit]
        [HttpPost("BatchUploadFile")]
        public List<long> BatchUploadFile([FromQuery][Required] string business, [FromQuery][Required] long key, [FromQuery][Required] string sign)
        {
            var fileIds = new List<long>();

            var ReqFiles = Request.Form.Files;


            List<IFormFile> Attachments = new();
            for (int i = 0; i < ReqFiles.Count; i++)
            {
                Attachments.Add(ReqFiles[i]);
            }

            foreach (var file in Attachments)
            {
                var fileId = UploadFile(business, key, sign, file);

                fileIds.Add(fileId);
            }

            return fileIds;
        }



        /// <summary>
        /// 通过文件ID获取文件
        /// </summary>
        /// <param name="fileid">文件ID</param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("GetFile")]
        public FileResult GetFile([Required] long fileid)
        {

            var file = db.TFile.Where(t => t.Id == fileid).FirstOrDefault();
            string path = Libraries.IO.Path.ContentRootPath() + file.Path;


            //读取文件入流
            var stream = System.IO.File.OpenRead(path);

            //获取文件后缀
            string fileExt = Path.GetExtension(path);

            //获取系统常规全部mime类型
            var provider = new FileExtensionContentTypeProvider();

            //通过文件后缀寻找对呀的mime类型
            var memi = provider.Mappings.ContainsKey(fileExt) ? provider.Mappings[fileExt] : provider.Mappings[".zip"];


            return File(stream, memi, file.Name);
        }




        /// <summary>
        /// 通过图片文件ID获取图片
        /// </summary>
        /// <param name="fileId">图片ID</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns></returns>
        /// <remarks>不指定宽高参数,返回原图</remarks>
        [AllowAnonymous]
        [HttpGet("GetImage")]
        public FileResult GetImage([Required] long fileId, int width, int height)
        {
            var file = db.TFile.Where(t => t.Id == fileId).FirstOrDefault();
            var path = Libraries.IO.Path.ContentRootPath() + file.Path;

            string fileExt = Path.GetExtension(path);
            var provider = new FileExtensionContentTypeProvider();
            var memi = provider.Mappings[fileExt];

            using var fileStream = System.IO.File.OpenRead(path);

            if (width == 0 && height == 0)
            {
                return File(fileStream, memi, file.Name);
            }
            else
            {

                using var original = SKBitmap.Decode(path);
                if (original.Width < width || original.Height < height)
                {
                    return File(fileStream, memi, file.Name);
                }
                else
                {

                    if (width != 0 && height == 0)
                    {
                        var percent = ((float)width / (float)original.Width);

                        width = (int)(original.Width * percent);
                        height = (int)(original.Height * percent);
                    }

                    if (width == 0 && height != 0)
                    {
                        var percent = ((float)height / (float)original.Height);

                        width = (int)(original.Width * percent);
                        height = (int)(original.Height * percent);
                    }

                    using var resizeBitmap = original.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
                    using var image = SKImage.FromBitmap(resizeBitmap);
                    using var imageData = image.Encode(SKEncodedImageFormat.Png, 100);
                    return File(imageData.ToArray(), "image/png");
                }
            }
        }



        /// <summary>
        /// 通过文件ID获取文件静态访问路径
        /// </summary>
        /// <param name="fileid">文件ID</param>
        /// <returns></returns>
        [HttpGet("GetFilePath")]
        public string GetFilePath([Required] long fileid)
        {

            var file = db.TFile.AsNoTracking().Where(t => t.IsDelete == false & t.Id == fileid).FirstOrDefault();

            if (file != null)
            {
                string domain = "https://file.xxxx.com";

                string fileUrl = domain + file.Path.Replace("\\", "/");

                return fileUrl;
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
                HttpContext.Items.Add("errMsg", "通过指定的文件ID未找到任何文件");

                return null;
            }

        }



        /// <summary>
        /// 多文件切片上传，获取初始化文件ID
        /// </summary>
        /// <param name="business">业务领域</param>
        /// <param name="key">记录值</param>
        /// <param name="sign">自定义标记</param>
        /// <param name="fileName">文件名称</param>
        /// <param name="slicing">总切片数</param>
        /// <param name="unique">文件校验值</param>
        /// <returns></returns>
        [HttpGet("CreateGroupFileId")]
        public long CreateGroupFileId([Required] string business, [Required] long key, [Required] string sign, [Required] string fileName, [Required] int slicing, [Required] string unique)
        {

            var dbfileinfo = db.TFileGroup.AsNoTracking().Where(t => t.IsDelete == false & t.Unique.ToLower() == unique.ToLower()).FirstOrDefault();

            if (dbfileinfo == null)
            {

                var fileid = Guid.NewGuid().ToString() + Path.GetExtension(fileName).ToLowerInvariant(); ;

                string basepath = "/Files/" + DateTime.UtcNow.ToString("yyyy/MM/dd") + "/" + fileid;


                TFile f = new();
                f.Id = snowflakeHelper.GetId();
                f.Name = fileName;
                f.Path = basepath;
                f.Table = business;
                f.TableId = key;
                f.Sign = sign;
                f.CreateTime = DateTime.UtcNow;

                db.TFile.Add(f);
                db.SaveChanges();

                TFileGroup group = new();
                group.Id = snowflakeHelper.GetId();
                group.FileId = f.Id;
                group.Unique = unique;
                group.Slicing = slicing;
                group.Issynthesis = false;
                group.Isfull = false;
                db.TFileGroup.Add(group);
                db.SaveChanges();

                return f.Id;
            }
            else
            {
                return dbfileinfo.FileId;
            }
        }



        /// <summary>
        /// 文件切片上传接口
        /// </summary>
        /// <param name="fileId">文件组ID</param>
        /// <param name="index">切片索引</param>
        /// <param name="file">file</param>
        /// <returns>文件ID</returns>
        [HttpPost("UploadGroupFile")]
        public bool UploadGroupFile([Required][FromForm] long fileId, [Required][FromForm] int index, [Required] IFormFile file)
        {

            try
            {
                var url = string.Empty;
                var fileName = string.Empty;
                var fileExtension = string.Empty;
                var fullFileName = string.Empty;

                string basepath = "/Files/Group/" + DateTime.UtcNow.ToString("yyyy/MM/dd") + "/" + fileId;
                string filepath = Libraries.IO.Path.ContentRootPath() + basepath;

                Directory.CreateDirectory(filepath);

                fileName = Guid.NewGuid().ToString();
                fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                fullFileName = string.Format("{0}{1}", fileName, fileExtension);

                string path = "";

                if (file != null && file.Length > 0)
                {
                    path = filepath + "/" + fullFileName;

                    using (FileStream fs = System.IO.File.Create(path))
                    {
                        file.CopyTo(fs);
                        fs.Flush();
                    }

                    path = basepath + "/" + fullFileName;
                }


                var group = db.TFileGroup.AsNoTracking().Where(t => t.IsDelete == false & t.FileId == fileId).FirstOrDefault();

                TFileGroupFile groupfile = new();
                groupfile.Id = snowflakeHelper.GetId();
                groupfile.FileId = group.FileId;
                groupfile.Path = path;
                groupfile.Index = index;
                groupfile.CreateTime = DateTime.UtcNow;

                db.TFileGroupFile.Add(groupfile);

                if (index == group.Slicing)
                {
                    group.Isfull = true;
                }

                db.SaveChanges();

                if (group.Isfull == true)
                {

                    byte[] buffer = new byte[1024 * 100];

                    var fileinfo = db.TFile.Where(t => t.Id == fileId).FirstOrDefault();

                    var fullfilepath = Libraries.IO.Path.ContentRootPath() + fileinfo.Path;

                    using (FileStream outStream = new(fullfilepath, FileMode.Create))
                    {
                        int readedLen = 0;
                        FileStream srcStream = null;

                        var filelist = db.TFileGroupFile.Where(t => t.FileId == fileinfo.Id).OrderBy(t => t.Index).ToList();

                        foreach (var item in filelist)
                        {
                            string p = Libraries.IO.Path.ContentRootPath() + item.Path;
                            srcStream = new FileStream(p, FileMode.Open);
                            while ((readedLen = srcStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                outStream.Write(buffer, 0, readedLen);
                            }
                            srcStream.Close();
                        }
                    }

                    group.Issynthesis = true;

                    db.SaveChanges();

                }

                return true;
            }
            catch
            {
                return false;
            }
        }



        /// <summary>
        /// 通过文件ID删除文件方法
        /// </summary>
        /// <param name="id">文件ID</param>
        /// <returns></returns>
        [HttpDelete("DeleteFile")]
        public bool DeleteFile(DtoId id)
        {

            var file = db.TFile.Where(t => t.IsDelete == false && t.Id == id.Id).FirstOrDefault();

            file.IsDelete = true;
            file.DeleteTime = DateTime.UtcNow;

            db.SaveChanges();

            return true;

        }


    }
}