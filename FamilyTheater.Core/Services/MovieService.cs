using FamilyTheater.Core.Data;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
    private const string AutoPosterSuffix = "_poster_auto";
    private const int AutoPosterHashLength = 12;
    private const double PosterCapturePositionRatio = 0.10;
    private static readonly Regex DurationPattern = new(
        @"Duration:\s*(?<duration>\d{2}:\d{2}:\d{2}(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILibraryDbContextFactory _dbContextFactory;
    private readonly ISettingService _settingService;
    private readonly IAppLogger _logger;

    public MovieService(ILibraryDbContextFactory dbContextFactory, ISettingService settingService, IAppLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _settingService = settingService;
        _logger = logger;
    }

    public async Task<ScanResult> ScanLibraryAsync(bool fullRescan = false)
    {
        var result = new ScanResult();

        var rootPath = await _settingService.GetMediaRootPathAsync();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            _logger.Warn($"电影库扫描跳过：媒体根目录无效。RootPath={rootPath}");
            return result;
        }

        var scanMode = fullRescan ? "完整" : "增量";
        _logger.Info($"开始{scanMode}扫描电影库：{rootPath}");

        var posterRootPath = await _settingService.GetMoviePosterRootPathAsync();
        Dictionary<string, string>? posterIndex = null;

        using var db = _dbContextFactory.CreateDbContext();
        var existingMovies = fullRescan
            ? await db.Movies
                .Include(m => m.MovieTags)
                .ToDictionaryAsync(m => m.VideoFilePath, m => m, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Movie>(StringComparer.OrdinalIgnoreCase);
        var existingVideoPaths = fullRescan
            ? existingMovies.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : (await db.Movies
                .AsNoTracking()
                .Select(m => m.VideoFilePath)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var discoveredVideos = 0;
        foreach (var videoFile in EnumerateVideoFilesRecursive(rootPath, result))
        {
            discoveredVideos++;

            var exists = existingVideoPaths.Contains(videoFile);
            if (!fullRescan && exists)
            {
                result.Skipped++;
                continue;
            }

            var folder = Path.GetDirectoryName(videoFile) ?? rootPath;
            var title = Path.GetFileNameWithoutExtension(videoFile);
            var autoPosterFolder = GetAutoPosterFolder(rootPath, posterRootPath, folder);
            posterIndex ??= BuildPosterIndex(posterRootPath);
            var posterFile = FindPosterFileForVideo(videoFile, posterIndex, autoPosterFolder);
            if (posterFile == null)
            {
                posterFile = await ExtractPosterFromVideoAsync(videoFile, autoPosterFolder, title);
            }

            var tags = ExtractTagsFromPath(rootPath, folder);

            if (exists && existingMovies.TryGetValue(videoFile, out var movie))
            {
                if (ShouldRefreshScannedTitle(movie))
                {
                    movie.Title = title;
                }

                movie.FolderPath = folder;
                movie.VideoFilePath = videoFile;
                movie.PosterPath = SelectPosterPath(movie.PosterPath, posterFile);
                movie.FileSizeBytes = GetFileSizeBytes(videoFile);
                movie.LastScannedAt = DateTime.UtcNow;
                SyncTags(movie, tags);
                result.Updated++;
            }
            else
            {
                var newMovie = new Movie
                {
                    Title = title,
                    FolderPath = folder,
                    VideoFilePath = videoFile,
                    PosterPath = posterFile,
                    FileSizeBytes = GetFileSizeBytes(videoFile),
                    LastScannedAt = DateTime.UtcNow
                };

                foreach (var tagName in tags)
                {
                    newMovie.MovieTags.Add(new MovieTag { Movie = newMovie, TagName = tagName });
                }

                db.Movies.Add(newMovie);
                existingMovies[videoFile] = newMovie;
                existingVideoPaths.Add(videoFile);
                result.Added++;
            }
        }

        if (discoveredVideos == 0)
        {
            _logger.Info($"电影库扫描结束：未找到视频文件。RootPath={rootPath}");
            return result;
        }

        if (result.Added > 0 || result.Updated > 0)
        {
            await db.SaveChangesAsync();
        }

        _logger.Info($"电影库{scanMode}扫描完成：新增 {result.Added}，更新 {result.Updated}，跳过 {result.Skipped}。RootPath={rootPath}");
        return result;
    }

    public async Task<List<Movie>> GetAllMoviesAsync()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.Movies
            .Include(m => m.MovieTags)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.MovieTags
            .Select(mt => mt.TagName)
            .Distinct()
            .OrderBy(name => name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Movie?> GetMovieByIdAsync(int movieId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.Movies
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

        using var db = _dbContextFactory.CreateDbContext();
        var movie = await db.Movies
            .Include(m => m.MovieTags)
            .FirstOrDefaultAsync(m => m.Id == movieId);
        if (movie == null)
        {
            _logger.Warn($"电影重命名失败：记录不存在。MovieId={movieId}");
            return null;
        }

        if (movie.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
            return movie;

        var oldVideoPath = movie.VideoFilePath;
        var folder = Path.GetDirectoryName(oldVideoPath);
        if (string.IsNullOrEmpty(folder))
        {
            _logger.Warn($"电影重命名失败：无法获取视频所在目录。MovieId={movieId}，VideoFilePath={oldVideoPath}");
            return null;
        }

        var extension = Path.GetExtension(oldVideoPath);
        var newVideoPath = Path.Combine(folder, title + extension);
        if (File.Exists(newVideoPath) &&
            !string.Equals(newVideoPath, oldVideoPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warn($"电影重命名失败：目标视频文件已存在。MovieId={movieId}，TargetPath={newVideoPath}");
            return null;
        }

        var newPosterPath = GetRenamedPosterPath(movie.PosterPath, title);
        if (!string.IsNullOrEmpty(newPosterPath) &&
            File.Exists(newPosterPath) &&
            !string.Equals(newPosterPath, movie.PosterPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warn($"电影重命名失败：目标海报文件已存在。MovieId={movieId}，TargetPath={newPosterPath}");
            return null;
        }

        try
        {
            File.Move(oldVideoPath, newVideoPath);
            _logger.Info($"电影视频文件已重命名：MovieId={movieId}，OldPath={oldVideoPath}，NewPath={newVideoPath}");

            if (!string.IsNullOrEmpty(newPosterPath) &&
                !string.Equals(newPosterPath, movie.PosterPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(movie.PosterPath!, newPosterPath);
                _logger.Info($"电影海报文件已重命名：MovieId={movieId}，OldPath={movie.PosterPath}，NewPath={newPosterPath}");
                movie.PosterPath = newPosterPath;
            }
        }
        catch
        {
            if (File.Exists(newVideoPath) && !File.Exists(oldVideoPath))
            {
                try
                {
                    File.Move(newVideoPath, oldVideoPath);
                }
                catch (Exception rollbackEx)
                {
                    _logger.Warn($"电影重命名回滚失败。MovieId={movieId}，OldPath={oldVideoPath}，NewPath={newVideoPath}", rollbackEx);
                }
            }

            throw;
        }

        movie.Title = title;
        movie.FolderPath = folder;
        movie.VideoFilePath = newVideoPath;
        movie.LastScannedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        _logger.Info($"电影记录已重命名：MovieId={movieId}，Title={title}");
        return movie;
    }

    public async Task<Movie?> SetPosterPathAsync(int movieId, string posterPath)
    {
        var path = posterPath.Trim();
        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path) ||
            !PosterExtensions.Contains(Path.GetExtension(path)))
        {
            _logger.Warn($"设置电影海报失败：海报文件无效。MovieId={movieId}, PosterPath={posterPath}");
            return null;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var movie = await db.Movies
            .Include(m => m.MovieTags)
            .FirstOrDefaultAsync(m => m.Id == movieId);
        if (movie == null)
        {
            _logger.Warn($"设置电影海报失败：记录不存在。MovieId={movieId}, PosterPath={path}");
            return null;
        }

        movie.PosterPath = path;
        movie.LastScannedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        _logger.Info($"电影海报已设置：MovieId={movieId}, PosterPath={path}");
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

        using var db = _dbContextFactory.CreateDbContext();
        var movie = await db.Movies
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
        await db.SaveChangesAsync();
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

        using var db = _dbContextFactory.CreateDbContext();
        var link = await db.MovieTags
            .FirstOrDefaultAsync(mt => mt.MovieId == movieId && mt.TagName == name);
        if (link == null)
        {
            _logger.Warn($"移除电影标签跳过：标签关系不存在。MovieId={movieId}，Tag={name}");
            return;
        }

        db.MovieTags.Remove(link);
        await db.SaveChangesAsync();
        _logger.Info($"电影标签已移除：MovieId={movieId}，Tag={name}");
    }

    public async Task DeleteTagAsync(string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn("删除电影标签跳过：标签为空");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var links = await db.MovieTags
            .Where(mt => mt.TagName == name)
            .ToListAsync();

        db.MovieTags.RemoveRange(links);
        await db.SaveChangesAsync();
        _logger.Info($"电影标签已删除：Tag={name}, Count={links.Count}");
    }

    public async Task DeleteMovieAsync(int movieId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var movie = await db.Movies
            .Include(m => m.MovieTags)
            .FirstOrDefaultAsync(m => m.Id == movieId);
        if (movie == null)
        {
            _logger.Warn($"删除电影记录跳过：记录不存在。MovieId={movieId}");
            return;
        }

        db.MovieTags.RemoveRange(movie.MovieTags);
        db.Movies.Remove(movie);
        await db.SaveChangesAsync();
        _logger.Info($"电影记录已删除：MovieId={movieId}，Title={movie.Title}，FolderPath={movie.FolderPath}");
    }

    private IEnumerable<string> EnumerateVideoFilesRecursive(string rootPath, ScanResult result)
    {
        using var enumerator = CreateRecursiveFileEnumerator(rootPath, result, "电影");
        if (enumerator == null)
        {
            yield break;
        }

        while (true)
        {
            string file;
            try
            {
                if (!enumerator.MoveNext())
                {
                    yield break;
                }

                file = enumerator.Current;
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Skipped++;
                _logger.Warn($"无权限读取电影目录。RootPath={rootPath}", ex);
                yield break;
            }
            catch (DirectoryNotFoundException ex)
            {
                result.Skipped++;
                _logger.Warn($"电影目录不存在。RootPath={rootPath}", ex);
                yield break;
            }

            if (VideoExtensions.Contains(Path.GetExtension(file)))
            {
                yield return file;
            }
        }
    }

    private Dictionary<string, string> BuildPosterIndex(string? posterRootPath)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(posterRootPath))
        {
            return index;
        }

        if (!Directory.Exists(posterRootPath))
        {
            _logger.Warn($"电影海报根目录无效，跳过独立海报扫描。PosterRootPath={posterRootPath}");
            return index;
        }

        var skipped = new ScanResult();
        using var enumerator = CreateRecursiveFileEnumerator(posterRootPath, skipped, "电影海报");
        if (enumerator == null)
        {
            return index;
        }

        while (true)
        {
            string file;
            try
            {
                if (!enumerator.MoveNext())
                {
                    return index;
                }

                file = enumerator.Current;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Warn($"无权限读取电影海报目录。PosterRootPath={posterRootPath}", ex);
                return index;
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.Warn($"电影海报目录不存在。PosterRootPath={posterRootPath}", ex);
                return index;
            }

            if (!PosterExtensions.Contains(Path.GetExtension(file)) || IsAutoGeneratedPosterFile(file))
            {
                continue;
            }

            AddPosterCandidate(index, Path.GetFileNameWithoutExtension(file), file);

            var baseName = Path.GetFileNameWithoutExtension(file);
            var parentName = Path.GetFileName(Path.GetDirectoryName(file));
            if (PosterNamePriority.Contains(baseName, StringComparer.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parentName))
            {
                AddPosterCandidate(index, parentName, file);
            }
        }
    }

    private static void AddPosterCandidate(Dictionary<string, string> index, string? key, string file)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        index.TryAdd(key, file);
    }

    private string? FindPosterFileForVideo(
        string videoFile,
        Dictionary<string, string> posterIndex,
        string autoPosterFolder)
    {
        var title = Path.GetFileNameWithoutExtension(videoFile);
        if (posterIndex.TryGetValue(title, out var indexedPoster))
        {
            return indexedPoster;
        }

        var folder = Path.GetDirectoryName(videoFile);
        if (string.IsNullOrEmpty(folder))
        {
            return null;
        }

        var localPoster = FindPosterFile(folder, title);
        if (localPoster != null)
        {
            return localPoster;
        }

        var autoPosterPath = Path.Combine(autoPosterFolder, $"{GetAutoPosterFileName(videoFile, title)}.jpg");
        return File.Exists(autoPosterPath) ? autoPosterPath : null;
    }

    private IEnumerator<string>? CreateRecursiveFileEnumerator(string rootPath, ScanResult result, string libraryName)
    {
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = 0
            };

            return Directory.EnumerateFiles(rootPath, "*", options).GetEnumerator();
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Skipped++;
            _logger.Warn($"无权限访问{libraryName}根目录：{rootPath}", ex);
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            result.Skipped++;
            _logger.Warn($"{libraryName}根目录不存在：{rootPath}", ex);
            return null;
        }
    }

    private static string GetAutoPosterFolder(string mediaRootPath, string? posterRootPath, string videoFolder)
    {
        if (string.IsNullOrWhiteSpace(posterRootPath) || !Directory.Exists(posterRootPath))
        {
            return videoFolder;
        }

        var relative = Path.GetRelativePath(mediaRootPath, videoFolder);
        if (relative == "." || relative.StartsWith(".."))
        {
            relative = string.Empty;
        }

        return Path.Combine(posterRootPath, "poster_auto", relative);
    }

    private long GetFileSizeBytes(string videoFile)
    {
        try
        {
            return new FileInfo(videoFile).Length;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Warn($"无权限读取电影文件大小：{videoFile}", ex);
            return 0;
        }
        catch (FileNotFoundException ex)
        {
            _logger.Warn($"电影文件不存在，无法读取大小：{videoFile}", ex);
            return 0;
        }
    }

    private static bool ShouldRefreshScannedTitle(Movie movie)
    {
        if (string.IsNullOrWhiteSpace(movie.Title))
        {
            return true;
        }

        var oldFolderName = Path.GetFileName(movie.FolderPath);
        if (string.IsNullOrWhiteSpace(oldFolderName))
        {
            return false;
        }

        return movie.Title.Equals(oldFolderName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetRenamedPosterPath(string? posterPath, string title)
    {
        if (string.IsNullOrWhiteSpace(posterPath) || !File.Exists(posterPath))
        {
            return null;
        }

        var folder = Path.GetDirectoryName(posterPath);
        if (string.IsNullOrEmpty(folder))
        {
            return null;
        }

        var extension = Path.GetExtension(posterPath);
        var oldName = Path.GetFileNameWithoutExtension(posterPath);
        var suffix = GetAutoPosterRenameSuffix(oldName);

        return Path.Combine(folder, $"{GetSafeFileName(title)}{suffix}{extension}");
    }

    private string? FindPosterFile(string folder, string? preferredTitle = null)
    {
        List<string> images;
        try
        {
            images = Directory.EnumerateFiles(folder, "*", CreateEnumerationOptions(recurse: false))
                .Where(f => PosterExtensions.Contains(Path.GetExtension(f)) && !IsAutoGeneratedPosterFile(f))
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

        if (!string.IsNullOrWhiteSpace(preferredTitle))
        {
            var preferredMatch = images.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(preferredTitle, StringComparison.OrdinalIgnoreCase));
            if (preferredMatch != null)
                return preferredMatch;
        }

        foreach (var priorityName in PosterNamePriority)
        {
            var match = images.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(priorityName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }

        return images[0];
    }

    private static EnumerationOptions CreateEnumerationOptions(bool recurse)
    {
        return new EnumerationOptions
        {
            RecurseSubdirectories = recurse,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = 0
        };
    }

    private async Task<string?> ExtractPosterFromVideoAsync(string videoPath, string folder, string title)
    {
        string? posterPath = null;

        try
        {
            if (!await FFmpegHelper.EnsureAvailableAsync(_logger))
            {
                _logger.Warn($"无法提取电影海报：FFmpeg 不可用。VideoPath={videoPath}");
                return null;
            }

            Directory.CreateDirectory(folder);
            posterPath = Path.Combine(folder, $"{GetAutoPosterFileName(videoPath, title)}.jpg");
            var duration = await GetVideoDurationAsync(videoPath);
            var capturePosition = duration.HasValue && duration.Value > TimeSpan.Zero
                ? TimeSpan.FromTicks((long)(duration.Value.Ticks * PosterCapturePositionRatio))
                : TimeSpan.Zero;

            var extraction = await RunPosterExtractionAsync(
                videoPath,
                posterPath,
                capturePosition,
                overrideColorMetadata: false);

            if (extraction.Success)
            {
                return posterPath;
            }

            DeleteIncompletePoster(posterPath);
            var compatibleExtraction = await RunPosterExtractionAsync(
                videoPath,
                posterPath,
                capturePosition,
                overrideColorMetadata: true);

            if (compatibleExtraction.Success)
            {
                _logger.Info($"FFmpeg 使用色彩兼容模式提取电影海报成功。VideoPath={videoPath}");
                return posterPath;
            }

            DeleteIncompletePoster(posterPath);
            _logger.Warn(
                $"FFmpeg 提取电影海报失败。VideoPath={videoPath}，" +
                $"CapturePosition={capturePosition.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s，" +
                $"ExitCode={extraction.ExitCode}，Error={extraction.Error}，Output={extraction.Output}，" +
                $"CompatibleExitCode={compatibleExtraction.ExitCode}，" +
                $"CompatibleError={compatibleExtraction.Error}，CompatibleOutput={compatibleExtraction.Output}");
            return null;
        }
        catch (Exception ex)
        {
            if (posterPath != null)
            {
                DeleteIncompletePoster(posterPath);
            }

            _logger.Warn($"提取电影海报发生异常。VideoPath={videoPath}", ex);
            return null;
        }
    }

    private async Task<TimeSpan?> GetVideoDurationAsync(string videoPath)
    {
        var startInfo = CreateFfmpegStartInfo();
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(videoPath);

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            _logger.Warn($"无法启动 FFmpeg 读取视频时长。VideoPath={videoPath}");
            return null;
        }

        var errorTask = process.StandardError.ReadToEndAsync();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var error = await errorTask;
        await outputTask;

        var durationText = DurationPattern.Match(error).Groups["duration"].Value;
        if (TimeSpan.TryParse(durationText, CultureInfo.InvariantCulture, out var duration) &&
            duration > TimeSpan.Zero)
        {
            return duration;
        }

        _logger.Warn($"无法读取视频时长，将从首帧提取海报。VideoPath={videoPath}");
        return null;
    }

    private static async Task<(bool Success, int ExitCode, string Error, string Output)> RunPosterExtractionAsync(
        string videoPath,
        string posterPath,
        TimeSpan capturePosition,
        bool overrideColorMetadata)
    {
        var startInfo = CreateFfmpegStartInfo();
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-ss");
        startInfo.ArgumentList.Add(capturePosition.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(videoPath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:v:0");
        startInfo.ArgumentList.Add("-frames:v");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add(overrideColorMetadata
            ? "setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709," +
              "scale=1284:-2:in_range=limited:in_color_matrix=bt709:out_color_matrix=bt709:out_range=full," +
              "format=yuvj420p"
            : "scale=1284:-2:out_range=full,format=yuvj420p");
        startInfo.ArgumentList.Add("-q:v");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add("-strict");
        startInfo.ArgumentList.Add("unofficial");
        startInfo.ArgumentList.Add("-update");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add(posterPath);

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return (false, int.MinValue, "无法启动 FFmpeg。", string.Empty);
        }

        var errorTask = process.StandardError.ReadToEndAsync();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var error = await errorTask;
        var output = await outputTask;
        var hasPoster = File.Exists(posterPath) && new FileInfo(posterPath).Length > 0;

        return (process.ExitCode == 0 && hasPoster, process.ExitCode, error, output);
    }

    private static ProcessStartInfo CreateFfmpegStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = FFmpegHelper.FfmpegExePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
    }

    private static void DeleteIncompletePoster(string posterPath)
    {
        try
        {
            if (File.Exists(posterPath))
            {
                File.Delete(posterPath);
            }
        }
        catch
        {
            // A failed cleanup must not interrupt the library scan.
        }
    }

    private static string GetAutoPosterFileName(string videoPath, string title)
    {
        var normalizedPath = Path.GetFullPath(videoPath).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)))
            .Substring(0, AutoPosterHashLength)
            .ToLowerInvariant();

        return $"{GetSafeFileName(title)}_{hash}{AutoPosterSuffix}";
    }

    private static string GetAutoPosterRenameSuffix(string oldName)
    {
        if (!oldName.EndsWith(AutoPosterSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var nameBeforeSuffix = oldName.Substring(0, oldName.Length - AutoPosterSuffix.Length);
        var hashSeparatorIndex = nameBeforeSuffix.LastIndexOf('_');
        if (hashSeparatorIndex < 0)
        {
            return AutoPosterSuffix;
        }

        var hash = nameBeforeSuffix.Substring(hashSeparatorIndex + 1);
        return IsAutoPosterHash(hash)
            ? $"_{hash}{AutoPosterSuffix}"
            : AutoPosterSuffix;
    }

    private static bool IsAutoPosterHash(string value)
    {
        return value.Length == AutoPosterHashLength &&
               value.All(ch => (ch >= '0' && ch <= '9') ||
                               (ch >= 'a' && ch <= 'f') ||
                               (ch >= 'A' && ch <= 'F'));
    }

    private static bool IsAutoGeneratedPosterFile(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return false;
        }

        return Path.GetFileNameWithoutExtension(file)
            .EndsWith(AutoPosterSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? SelectPosterPath(string? existingPosterPath, string? scannedPosterPath)
    {
        if (ShouldKeepExistingPoster(existingPosterPath) &&
            !IsAutoGeneratedPosterFile(existingPosterPath))
        {
            return existingPosterPath;
        }

        return scannedPosterPath;
    }

    private static bool ShouldKeepExistingPoster(string? posterPath)
    {
        return !string.IsNullOrWhiteSpace(posterPath) &&
               File.Exists(posterPath) &&
               PosterExtensions.Contains(Path.GetExtension(posterPath));
    }

    private static string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeChars = fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var safeName = new string(safeChars).Trim();
        return string.IsNullOrEmpty(safeName) ? "poster" : safeName;
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

        return parts.ToList();
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
