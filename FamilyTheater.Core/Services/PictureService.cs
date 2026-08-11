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
                .ThenInclude(pt => pt.Tag)
            .ToDictionaryAsync(p => p.FilePath, p => p, StringComparer.OrdinalIgnoreCase);

        // 一次性加载已有 Tag（按 Name 索引）
        var existingTags = await _db.Tags
            .ToDictionaryAsync(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

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

            // 确保标签存在
            if (!existingTags.TryGetValue(tagName, out var tag))
            {
                tag = new Tag { Name = tagName };
                _db.Tags.Add(tag);
                existingTags[tagName] = tag;
            }

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
                        pt.Tag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
                    {
                        picture.PictureTags.Add(new PictureTag { Picture = picture, Tag = tag });
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

                    newPicture.PictureTags.Add(new PictureTag { Picture = newPicture, Tag = tag });

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
                .ThenInclude(pt => pt.Tag)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Tag>> GetAllTagsAsync()
    {
        // 只返回被图片使用过的标签
        return await _db.Tags
            .Where(t => t.PictureTags.Any())
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Picture?> GetPictureByIdAsync(int pictureId)
    {
        return await _db.Pictures
            .Include(p => p.PictureTags)
                .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == pictureId);
    }

    public async Task AddTagToPictureAsync(int pictureId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
            return;

        var picture = await _db.Pictures
            .Include(p => p.PictureTags)
                .ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == pictureId);
        if (picture == null)
            return;

        // 已有该标签则跳过
        if (picture.PictureTags.Any(pt =>
            pt.Tag.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        // 查找或创建 Tag
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == name);
        if (tag == null)
        {
            tag = new Tag { Name = name };
            _db.Tags.Add(tag);
        }

        picture.PictureTags.Add(new PictureTag { Picture = picture, Tag = tag });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveTagFromPictureAsync(int pictureId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
            return;

        var link = await _db.PictureTags
            .Include(pt => pt.Tag)
            .FirstOrDefaultAsync(pt =>
                pt.PictureId == pictureId &&
                pt.Tag.Name == name);
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
