using FamilyTheater.Core.Data;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class MovieService : IMovieService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".wmv", ".flv", ".mov", ".rmvb", ".ts", ".m4v", ".webm"
    };

    private static readonly HashSet<string> PosterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    private static readonly string[] PosterNamePriority = { "poster", "cover", "folder" };

    private readonly AppDbContext _db;
    private readonly ISettingService _settingService;
    private readonly IAppLogger _logger;

    public MovieService(AppDbContext db, ISettingService settingService, IAppLogger logger)
    {
        _db = db;
        _settingService = settingService;
        _logger = logger;
    }

    public async Task<ScanResult> ScanLibraryAsync()
    {
        var result = new ScanResult();

        var rootPath = await _settingService.GetMediaRootPathAsync();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            _logger.Warn($"电影库扫描跳过：媒体根目录无效。RootPath={rootPath}");
            return result;
        }

        _logger.Info($"开始扫描电影库：{rootPath}");

        var leafFolders = FindLeafFolders(rootPath);
        if (leafFolders.Count == 0)
        {
            _logger.Info($"电影库扫描结束：未找到叶子文件夹。RootPath={rootPath}");
            return result;
        }

        var existingMovies = await _db.Movies
            .Include(m => m.MovieTags)
            .ToDictionaryAsync(m => m.FolderPath, m => m, StringComparer.OrdinalIgnoreCase);

        foreach (var folder in leafFolders)
        {
            var videoFile = FindVideoFile(folder);
            if (videoFile == null)
            {
                result.Skipped++;
                continue;
            }

            var posterFile = FindPosterFile(folder);
            if (posterFile == null)
            {
                posterFile = await ExtractPosterFromVideoAsync(videoFile, folder);
            }

            var tags = ExtractTagsFromPath(rootPath, folder);

            if (existingMovies.TryGetValue(folder, out var movie))
            {
                movie.VideoFilePath = videoFile;
                movie.PosterPath = posterFile;
                movie.FileSizeBytes = new FileInfo(videoFile).Length;
                movie.LastScannedAt = DateTime.UtcNow;
                SyncTags(movie, tags);
                result.Updated++;
            }
            else
            {
                var newMovie = new Movie
                {
                    Title = Path.GetFileName(folder),
                    FolderPath = folder,
                    VideoFilePath = videoFile,
                    PosterPath = posterFile,
                    FileSizeBytes = new FileInfo(videoFile).Length,
                    LastScannedAt = DateTime.UtcNow
                };

                foreach (var tagName in tags)
                {
                    newMovie.MovieTags.Add(new MovieTag { Movie = newMovie, TagName = tagName });
                }

                _db.Movies.Add(newMovie);
                existingMovies[folder] = newMovie;
                result.Added++;
            }
        }

        await _db.SaveChangesAsync();
        _logger.Info($"电影库扫描完成：新增 {result.Added}，更新 {result.Updated}，跳过 {result.Skipped}。RootPath={rootPath}");
        return result;
    }

    public async Task<List<Movie>> GetAllMoviesAsync()
    {
        return await _db.Movies
            .Include(m => m.MovieTags)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        return await _db.MovieTags
            .Select(mt => mt.TagName)
            .Distinct()
            .OrderBy(name => name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Movie?> GetMovieByIdAsync(int movieId)
    {
        return await _db.Movies
            .Include(m => m.MovieTags)
            .FirstOrDefaultAsync(m => m.Id == movieId);
    }

    public async Task<Movie?> RenameMovieAsync(int movieId, string newTitle)
    {
        var title = newTitle.Trim();
        if (string.IsNullOrEmpty(title))
        {
            _logger.Warn($"电影重命名失败：新标题为空。MovieId={movieId}");
            return null;
        }

        var movie = await _db.Movies
            .Include(m => m.MovieTags)
            .FirstOrDefaultAsync(m => m.Id == movieId);
        if (movie == null)
        {
            _logger.Warn($"电影重命名失败：记录不存在。MovieId={movieId}");
            return null;
        }

        if (movie.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
            return movie;

        var oldFolderPath = movie.FolderPath;
        var parentDir = Path.GetDirectoryName(oldFolderPath);
        if (string.IsNullOrEmpty(parentDir))
        {
            _logger.Warn($"电影重命名失败：无法获取父目录。MovieId={movieId}，FolderPath={oldFolderPath}");
            return null;
        }

        var newFolderPath = Path.Combine(parentDir, title);
        if (Directory.Exists(newFolderPath) &&
            !string.Equals(newFolderPath, oldFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warn($"电影重命名失败：目标文件夹已存在。MovieId={movieId}，TargetPath={newFolderPath}");
            return null;
        }

        Directory.Move(oldFolderPath, newFolderPath);
        _logger.Info($"电影文件夹已重命名：MovieId={movieId}，OldPath={oldFolderPath}，NewPath={newFolderPath}");

        movie.Title = title;
        movie.FolderPath = newFolderPath;

        if (!string.IsNullOrEmpty(movie.VideoFilePath) &&
            movie.VideoFilePath.StartsWith(oldFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            movie.VideoFilePath = Path.Combine(
                newFolderPath,
                movie.VideoFilePath.Substring(oldFolderPath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (!string.IsNullOrEmpty(movie.PosterPath) &&
            movie.PosterPath.StartsWith(oldFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            movie.PosterPath = Path.Combine(
                newFolderPath,
                movie.PosterPath.Substring(oldFolderPath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        movie.LastScannedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.Info($"电影记录已重命名：MovieId={movieId}，Title={title}");
        return movie;
    }

    public async Task AddTagToMovieAsync(int movieId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn($"添加电影标签跳过：标签为空。MovieId={movieId}");
            return;
        }

        var movie = await _db.Movies
            .Include(m => m.MovieTags)
            .FirstOrDefaultAsync(m => m.Id == movieId);
        if (movie == null)
        {
            _logger.Warn($"添加电影标签失败：记录不存在。MovieId={movieId}，Tag={name}");
            return;
        }

        if (movie.MovieTags.Any(mt => mt.TagName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        movie.MovieTags.Add(new MovieTag { Movie = movie, TagName = name });
        await _db.SaveChangesAsync();
        _logger.Info($"电影标签已添加：MovieId={movieId}，Tag={name}");
    }

    public async Task RemoveTagFromMovieAsync(int movieId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn($"移除电影标签跳过：标签为空。MovieId={movieId}");
            return;
        }

        var link = await _db.MovieTags
            .FirstOrDefaultAsync(mt => mt.MovieId == movieId && mt.TagName == name);
        if (link == null)
        {
            _logger.Warn($"移除电影标签跳过：标签关系不存在。MovieId={movieId}，Tag={name}");
            return;
        }

        _db.MovieTags.Remove(link);
        await _db.SaveChangesAsync();
        _logger.Info($"电影标签已移除：MovieId={movieId}，Tag={name}");
    }

    public async Task DeleteMovieAsync(int movieId)
    {
        var movie = await _db.Movies
            .Include(m => m.MovieTags)
            .FirstOrDefaultAsync(m => m.Id == movieId);
        if (movie == null)
        {
            _logger.Warn($"删除电影记录跳过：记录不存在。MovieId={movieId}");
            return;
        }

        _db.MovieTags.RemoveRange(movie.MovieTags);
        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync();
        _logger.Info($"电影记录已删除：MovieId={movieId}，Title={movie.Title}，FolderPath={movie.FolderPath}");
    }

    private List<string> FindLeafFolders(string root)
    {
        var result = new List<string>();
        FindLeafFoldersCore(root, result);
        return result;
    }

    private void FindLeafFoldersCore(string current, List<string> result)
    {
        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(current);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Warn($"无权限访问电影目录：{current}", ex);
            return;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.Warn($"电影目录不存在：{current}", ex);
            return;
        }

        if (subDirs.Length == 0)
        {
            result.Add(current);
            return;
        }

        foreach (var sub in subDirs)
            FindLeafFoldersCore(sub, result);
    }

    private string? FindVideoFile(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder)
                .FirstOrDefault(f => VideoExtensions.Contains(Path.GetExtension(f)));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Warn($"无权限读取电影文件夹：{folder}", ex);
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.Warn($"电影文件夹不存在：{folder}", ex);
            return null;
        }
    }

    private string? FindPosterFile(string folder)
    {
        List<string> images;
        try
        {
            images = Directory.EnumerateFiles(folder)
                .Where(f => PosterExtensions.Contains(Path.GetExtension(f)))
                .ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Warn($"无权限读取电影海报文件夹：{folder}", ex);
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.Warn($"电影海报文件夹不存在：{folder}", ex);
            return null;
        }

        if (images.Count == 0)
            return null;

        foreach (var priorityName in PosterNamePriority)
        {
            var match = images.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(priorityName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }

        return images[0];
    }

    private async Task<string?> ExtractPosterFromVideoAsync(string videoPath, string folder)
    {
        try
        {
            if (!await FFmpegHelper.EnsureAvailableAsync(_logger))
            {
                _logger.Warn($"无法提取电影海报：FFmpeg 不可用。VideoPath={videoPath}");
                return null;
            }

            var posterPath = Path.Combine(folder, "poster_auto.jpg");
            var args = $"-y -ss 10 -i \"{videoPath}\" -vframes 1 -vf \"scale=1284:-1\" \"{posterPath}\"";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = FFmpegHelper.FfmpegExePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
            {
                _logger.Warn($"无法启动 FFmpeg 提取电影海报。VideoPath={videoPath}");
                return null;
            }

            await proc.WaitForExitAsync();
            var error = proc.StandardError.ReadToEnd();
            var output = proc.StandardOutput.ReadToEnd();

            if (proc.ExitCode != 0)
            {
                _logger.Warn($"FFmpeg 提取电影海报失败。ExitCode={proc.ExitCode}，VideoPath={videoPath}，Error={error}，Output={output}");
                return null;
            }

            return File.Exists(posterPath) ? posterPath : null;
        }
        catch (Exception ex)
        {
            _logger.Warn($"提取电影海报发生异常。VideoPath={videoPath}", ex);
            return null;
        }
    }

    private static List<string> ExtractTagsFromPath(string root, string folder)
    {
        var rootUri = new DirectoryInfo(root).FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folderUri = new DirectoryInfo(folder).FullName;

        string relative;
        if (folderUri.StartsWith(rootUri, StringComparison.OrdinalIgnoreCase))
            relative = folderUri.Substring(rootUri.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        else
            relative = Path.GetFileName(folder);

        var parts = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 1
            ? parts.Take(parts.Length - 1).ToList()
            : new List<string>();
    }

    private static void SyncTags(Movie movie, List<string> tagNames)
    {
        var currentTagNames = movie.MovieTags
            .Select(mt => mt.TagName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tagName in tagNames)
        {
            if (currentTagNames.Contains(tagName))
                continue;

            movie.MovieTags.Add(new MovieTag { Movie = movie, TagName = tagName });
        }
    }
}
