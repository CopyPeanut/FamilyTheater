using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class PictureService : IPictureService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    private readonly ILibraryDbContextFactory _dbContextFactory;
    private readonly ISettingService _settingService;
    private readonly IAppLogger _logger;

    public PictureService(ILibraryDbContextFactory dbContextFactory, ISettingService settingService, IAppLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _settingService = settingService;
        _logger = logger;
    }

    public async Task<ScanResult> ScanLibraryAsync()
    {
        var result = new ScanResult();

        var rootPath = await _settingService.GetPictureRootPathAsync();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            _logger.Warn($"图片库扫描跳过：图片根目录无效。RootPath={rootPath}");
            return result;
        }

        _logger.Info($"开始扫描图片库：{rootPath}");

        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(rootPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Warn($"无权限访问图片根目录：{rootPath}", ex);
            return result;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.Warn($"图片根目录不存在：{rootPath}", ex);
            return result;
        }

        if (subDirs.Length == 0)
        {
            _logger.Info($"图片库扫描结束：未找到子文件夹。RootPath={rootPath}");
            return result;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var existingPictures = await db.Pictures
            .Include(p => p.PictureTags)
            .ToDictionaryAsync(p => p.FilePath, p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var subDir in subDirs)
        {
            var tagName = Path.GetFileName(subDir);

            List<string> imageFiles;
            try
            {
                imageFiles = Directory.EnumerateFiles(subDir)
                    .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
                    .ToList();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Warn($"无权限读取图片文件夹：{subDir}", ex);
                continue;
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.Warn($"图片文件夹不存在：{subDir}", ex);
                continue;
            }

            if (imageFiles.Count == 0)
            {
                result.Skipped++;
                continue;
            }

            foreach (var imageFile in imageFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(imageFile);

                if (existingPictures.TryGetValue(imageFile, out var picture))
                {
                    picture.FileName = fileName;
                    picture.FolderPath = subDir;
                    picture.FileSizeBytes = new FileInfo(imageFile).Length;
                    picture.LastScannedAt = DateTime.UtcNow;

                    if (!picture.PictureTags.Any(pt => pt.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
                    {
                        picture.PictureTags.Add(new PictureTag { Picture = picture, TagName = tagName });
                    }

                    result.Updated++;
                }
                else
                {
                    var newPicture = new Picture
                    {
                        FilePath = imageFile,
                        FileName = fileName,
                        FolderPath = subDir,
                        FileSizeBytes = new FileInfo(imageFile).Length,
                        LastScannedAt = DateTime.UtcNow
                    };

                    newPicture.PictureTags.Add(new PictureTag { Picture = newPicture, TagName = tagName });

                    db.Pictures.Add(newPicture);
                    existingPictures[imageFile] = newPicture;
                    result.Added++;
                }
            }
        }

        await db.SaveChangesAsync();
        _logger.Info($"图片库扫描完成：新增 {result.Added}，更新 {result.Updated}，跳过 {result.Skipped}。RootPath={rootPath}");
        return result;
    }

    public async Task<List<Picture>> GetAllPicturesAsync()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.Pictures
            .Include(p => p.PictureTags)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.PictureTags
            .Select(pt => pt.TagName)
            .Distinct()
            .OrderBy(name => name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Picture?> GetPictureByIdAsync(int pictureId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.Pictures
            .Include(p => p.PictureTags)
            .FirstOrDefaultAsync(p => p.Id == pictureId);
    }

    public async Task AddTagToPictureAsync(int pictureId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn($"添加图片标签跳过：标签为空。PictureId={pictureId}");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var picture = await db.Pictures
            .Include(p => p.PictureTags)
            .FirstOrDefaultAsync(p => p.Id == pictureId);
        if (picture == null)
        {
            _logger.Warn($"添加图片标签失败：记录不存在。PictureId={pictureId}，Tag={name}");
            return;
        }

        if (picture.PictureTags.Any(pt => pt.TagName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        picture.PictureTags.Add(new PictureTag { Picture = picture, TagName = name });
        await db.SaveChangesAsync();
        _logger.Info($"图片标签已添加：PictureId={pictureId}，Tag={name}");
    }

    public async Task RemoveTagFromPictureAsync(int pictureId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn($"移除图片标签跳过：标签为空。PictureId={pictureId}");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var link = await db.PictureTags
            .FirstOrDefaultAsync(pt => pt.PictureId == pictureId && pt.TagName == name);
        if (link == null)
        {
            _logger.Warn($"移除图片标签跳过：标签关系不存在。PictureId={pictureId}，Tag={name}");
            return;
        }

        db.PictureTags.Remove(link);
        await db.SaveChangesAsync();
        _logger.Info($"图片标签已移除：PictureId={pictureId}，Tag={name}");
    }

    public async Task DeletePictureAsync(int pictureId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var picture = await db.Pictures
            .Include(p => p.PictureTags)
            .FirstOrDefaultAsync(p => p.Id == pictureId);
        if (picture == null)
        {
            _logger.Warn($"删除图片记录跳过：记录不存在。PictureId={pictureId}");
            return;
        }

        db.PictureTags.RemoveRange(picture.PictureTags);
        db.Pictures.Remove(picture);
        await db.SaveChangesAsync();
        _logger.Info($"图片记录已删除：PictureId={pictureId}，FilePath={picture.FilePath}");
    }
}
