using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Services;

public class MovieService : IMovieService
{
    private readonly AppDbContext _db;
    private readonly ISettingService _settingService;

    /// <summary>支持的视频扩展名</summary>
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".wmv", ".flv", ".mov", ".rmvb", ".ts", ".m4v", ".webm"
    };

    /// <summary>支持的海报图片扩展名</summary>
    private static readonly HashSet<string> PosterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    /// <summary>海报文件名优先级（不含扩展名），越小越优先</summary>
    private static readonly string[] PosterNamePriority = { "poster", "cover", "folder" };

    public MovieService(AppDbContext db, ISettingService settingService)
    {
        _db = db;
        _settingService = settingService;
    }

    public async Task<ScanResult> ScanLibraryAsync()
    {
        var result = new ScanResult();

        var rootPath = await _settingService.GetMediaRootPathAsync();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return result;

        // 找出所有叶子文件夹（不含子目录的文件夹）
        var leafFolders = FindLeafFolders(rootPath);
        if (leafFolders.Count == 0)
            return result;

        // 一次性加载已有 Movie（按 FolderPath 索引），避免逐个查询
        var existingMovies = await _db.Movies
            .Include(m => m.MovieTags)
            .ToDictionaryAsync(m => m.FolderPath, m => m, StringComparer.OrdinalIgnoreCase);

        // 一次性加载已有 Tag（按 Name 索引），避免重复创建
        var existingTags = await _db.Tags
            .ToDictionaryAsync(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        foreach (var folder in leafFolders)
        {
            var videoFile = FindVideoFile(folder);
            if (videoFile == null)
            {
                // 叶子文件夹里没有视频文件，跳过
                result.Skipped++;
                continue;
            }

            var posterFile = FindPosterFile(folder);
            var tags = ExtractTagsFromPath(rootPath, folder);

            if (existingMovies.TryGetValue(folder, out var movie))
            {
                // 更新已有记录
                movie.VideoFilePath = videoFile;
                movie.PosterPath = posterFile;
                movie.FileSizeBytes = new FileInfo(videoFile).Length;
                movie.LastScannedAt = DateTime.UtcNow;

                // 同步标签（保留已有的，添加新的）
                SyncTags(movie, tags, existingTags);
                result.Updated++;
            }
            else
            {
                // 新增记录
                var newMovie = new Movie
                {
                    Title = Path.GetFileName(folder),
                    FolderPath = folder,
                    VideoFilePath = videoFile,
                    PosterPath = posterFile,
                    FileSizeBytes = new FileInfo(videoFile).Length,
                    LastScannedAt = DateTime.UtcNow
                };

                // 附加标签
                foreach (var tagName in tags)
                {
                    if (!existingTags.TryGetValue(tagName, out var tag))
                    {
                        tag = new Tag { Name = tagName };
                        _db.Tags.Add(tag);
                        existingTags[tagName] = tag;
                    }
                    newMovie.MovieTags.Add(new MovieTag { Movie = newMovie, Tag = tag });
                }

                _db.Movies.Add(newMovie);
                existingMovies[folder] = newMovie;
                result.Added++;
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<List<Movie>> GetAllMoviesAsync()
    {
        return await _db.Movies
            .Include(m => m.MovieTags)
                .ThenInclude(mt => mt.Tag)
            .AsNoTracking()
            .ToListAsync();
    }

    // ────────────────────────── 私有方法 ──────────────────────────

    /// <summary>
    /// 递归找出所有叶子文件夹（不含子目录的文件夹）。
    /// </summary>
    private static List<string> FindLeafFolders(string root)
    {
        var result = new List<string>();
        FindLeafFoldersCore(root, result);
        return result;
    }

    private static void FindLeafFoldersCore(string current, List<string> result)
    {
        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(current);
        }
        catch (UnauthorizedAccessException) { return; }
        catch (DirectoryNotFoundException) { return; }

        if (subDirs.Length == 0)
        {
            // 叶子文件夹
            result.Add(current);
        }
        else
        {
            foreach (var sub in subDirs)
                FindLeafFoldersCore(sub, result);
        }
    }

    /// <summary>
    /// 在文件夹中找第一个视频文件。
    /// </summary>
    private static string? FindVideoFile(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder)
                .FirstOrDefault(f => VideoExtensions.Contains(Path.GetExtension(f)));
        }
        catch (UnauthorizedAccessException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    /// <summary>
    /// 在文件夹中找海报图片，优先级：poster/cover/folder > 其余图片。
    /// </summary>
    private static string? FindPosterFile(string folder)
    {
        List<string> images;
        try
        {
            images = Directory.EnumerateFiles(folder)
                .Where(f => PosterExtensions.Contains(Path.GetExtension(f)))
                .ToList();
        }
        catch (UnauthorizedAccessException) { return null; }
        catch (DirectoryNotFoundException) { return null; }

        if (images.Count == 0)
            return null;

        // 按优先级匹配文件名（不含扩展名）
        foreach (var priorityName in PosterNamePriority)
        {
            var match = images.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(priorityName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }

        // 没有命名优先项，取第一张图片
        return images[0];
    }

    /// <summary>
    /// 从路径中提取标签：取根目录之后的各层文件夹名作为标签。
    /// 例：root=d:\movie, folder=d:\movie\english\anime\superman → [english, anime, superman]
    /// 注意：最后一层（电影文件夹名）不作为标签，因为它已被用作 Title。
    /// </summary>
    private static List<string> ExtractTagsFromPath(string root, string folder)
    {
        var rootUri = new DirectoryInfo(root).FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folderUri = new DirectoryInfo(folder).FullName;

        // 获取相对路径
        string relative;
        if (folderUri.StartsWith(rootUri, StringComparison.OrdinalIgnoreCase))
            relative = folderUri.Substring(rootUri.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        else
            relative = Path.GetFileName(folder);

        var parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                                   StringSplitOptions.RemoveEmptyEntries);

        // 去掉最后一层（电影文件夹名，已用作 Title），其余作为标签
        return parts.Length > 1
            ? parts.Take(parts.Length - 1).ToList()
            : new List<string>();
    }

    /// <summary>
    /// 同步已有 Movie 的标签：保留已有的，添加新的。
    /// </summary>
    private void SyncTags(Movie movie, List<string> tagNames, Dictionary<string, Tag> existingTags)
    {
        var currentTagNames = movie.MovieTags
            .Select(mt => mt.Tag.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tagName in tagNames)
        {
            if (currentTagNames.Contains(tagName))
                continue; // 已有该标签

            if (!existingTags.TryGetValue(tagName, out var tag))
            {
                tag = new Tag { Name = tagName };
                _db.Tags.Add(tag);
                existingTags[tagName] = tag;
            }

            movie.MovieTags.Add(new MovieTag { Movie = movie, Tag = tag });
        }
    }
}
