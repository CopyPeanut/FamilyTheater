using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Services;

public class PictureService : IPictureService
{
    private readonly AppDbContext _db;
    private readonly ISettingService _settingService;

    /// <summary>支持的图片扩展名</summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    public PictureService(AppDbContext db, ISettingService settingService)
    {
        _db = db;
        _settingService = settingService;
    }

    public async Task<ScanResult> ScanLibraryAsync()
    {
        var result = new ScanResult();

        var rootPath = await _settingService.GetPictureRootPathAsync();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return result;

        // 遍历根目录下的子文件夹（仅一层）
        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(rootPath);
        }
        catch (UnauthorizedAccessException) { return result; }
        catch (DirectoryNotFoundException) { return result; }

        if (subDirs.Length == 0)
            return result;

        // 一次性加载已有 Picture（按 FilePath 索引）
        var existingPictures = await _db.Pictures
            .Include(p => p.PictureTags)
            .ToDictionaryAsync(p => p.FilePath, p => p, StringComparer.OrdinalIgnoreCase);

        // 标签直接存为 PictureTag.TagName，无需独立 Tag 表

        foreach (var subDir in subDirs)
        {
            var tagName = Path.GetFileName(subDir);

            // 扫描子文件夹里的所有图片文件
            List<string> imageFiles;
            try
            {
                imageFiles = Directory.EnumerateFiles(subDir)
                    .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
                    .ToList();
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }

            if (imageFiles.Count == 0)
            {
                result.Skipped++;
                continue;
            }

            // 标签名直接写入 PictureTag，无需 Tag 表

            foreach (var imageFile in imageFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(imageFile);

                if (existingPictures.TryGetValue(imageFile, out var picture))
                {
                    // 更新已有记录
                    picture.FileName = fileName;
                    picture.FolderPath = subDir;
                    picture.FileSizeBytes = new FileInfo(imageFile).Length;
                    picture.LastScannedAt = DateTime.UtcNow;

                    // 同步标签（确保标签关联存在）
                    if (!picture.PictureTags.Any(pt =>
                        pt.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
                    {
                        picture.PictureTags.Add(new PictureTag { Picture = picture, TagName = tagName });
                    }
                    result.Updated++;
                }
                else
                {
                    // 新增记录
                    var newPicture = new Picture
                    {
                        FilePath = imageFile,
                        FileName = fileName,
                        FolderPath = subDir,
                        FileSizeBytes = new FileInfo(imageFile).Length,
                        LastScannedAt = DateTime.UtcNow
                    };

                    newPicture.PictureTags.Add(new PictureTag { Picture = newPicture, TagName = tagName });

                    _db.Pictures.Add(newPicture);
                    existingPictures[imageFile] = newPicture;
                    result.Added++;
                }
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<List<Picture>> GetAllPicturesAsync()
    {
        return await _db.Pictures
            .Include(p => p.PictureTags)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<string>> GetAllTagsAsync()
        {
            return await _db.PictureTags
                .Select(pt => pt.TagName)
                .Distinct()
                .OrderBy(name => name)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Picture?> GetPictureByIdAsync(int pictureId)
    {
        return await _db.Pictures
            .Include(p => p.PictureTags)
            .FirstOrDefaultAsync(p => p.Id == pictureId);
    }

    public async Task AddTagToPictureAsync(int pictureId, string tagName)
        {
            var name = tagName.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            var picture = await _db.Pictures
                .Include(p => p.PictureTags)
                .FirstOrDefaultAsync(p => p.Id == pictureId);
            if (picture == null)
                return;

            // 已有该标签则跳过
            if (picture.PictureTags.Any(pt =>
                pt.TagName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return;

            picture.PictureTags.Add(new PictureTag { Picture = picture, TagName = name });
            await _db.SaveChangesAsync();
        }

        public async Task RemoveTagFromPictureAsync(int pictureId, string tagName)
        {
            var name = tagName.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            var link = await _db.PictureTags
                .FirstOrDefaultAsync(pt =>
                    pt.PictureId == pictureId &&
                    pt.TagName == name);
            if (link == null)
                return;

            _db.PictureTags.Remove(link);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// 删除图片记录及其所有标签关联（PictureTag），不删除 Tag 记录本身。
        /// </summary>
        public async Task DeletePictureAsync(int pictureId)
        {
            var picture = await _db.Pictures
                .Include(p => p.PictureTags)
                .FirstOrDefaultAsync(p => p.Id == pictureId);
            if (picture == null)
                return;

            // 删除所有标签关联
            _db.PictureTags.RemoveRange(picture.PictureTags);
            // 删除图片记录
            _db.Pictures.Remove(picture);
            await _db.SaveChangesAsync();
        }
    }
