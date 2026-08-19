using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class GameService : IGameService
{
    private static readonly HashSet<string> PosterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    private static readonly HashSet<string> ScreenshotExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
    };

    private static readonly string[] PosterNamePriority = { "poster", "cover", "folder" };

    private static readonly string[] ExcludedExecutableNameParts =
    {
        "unins", "uninstall", "setup", "install", "installer", "crash",
        "report", "redist", "vcredist", "dxsetup", "dotnet", "unitycrashhandler"
    };

    private static readonly string[] ExcludedExecutablePathParts =
    {
        "redist", "_commonredist", "support", "installer", "installers",
        "crash", "tools", "vc_redist", "directx"
    };

    private readonly ILibraryDbContextFactory _dbContextFactory;
    private readonly ISettingService _settingService;
    private readonly IAppLogger _logger;

    public GameService(ILibraryDbContextFactory dbContextFactory, ISettingService settingService, IAppLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _settingService = settingService;
        _logger = logger;
    }

    public async Task<ScanResult> ScanLibraryAsync()
    {
        var result = new ScanResult();
        var rootPath = await _settingService.GetGameRootPathAsync();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            _logger.Warn($"游戏库扫描跳过：游戏根目录无效。RootPath={rootPath}");
            return result;
        }

        var posterRootPath = await _settingService.GetGamePosterRootPathAsync();
        var posterIndex = BuildPosterIndex(posterRootPath);

        using var db = _dbContextFactory.CreateDbContext();
        var existingGames = await db.Games
            .Include(g => g.GameTags)
            .ToDictionaryAsync(g => g.FolderPath, g => g, StringComparer.OrdinalIgnoreCase);

        var folders = FindGameFolders(rootPath);
        foreach (var folder in folders)
        {
            var title = Path.GetFileName(folder);
            var posterPath = ResolvePosterFile(folder, title, posterRootPath, posterIndex);
            var tags = ExtractTagsFromPath(rootPath, folder);

            if (existingGames.TryGetValue(folder, out var game))
            {
                game.Title = string.IsNullOrWhiteSpace(game.Title) ? title : game.Title;
                game.PosterPath = posterPath ?? game.PosterPath;
                game.FolderSizeBytes = 0;
                game.LastScannedAt = DateTime.UtcNow;
                SyncTags(game, tags);
                result.Updated++;
            }
            else
            {
                var newGame = new Game
                {
                    Title = title,
                    FolderPath = folder,
                    PosterPath = posterPath,
                    FolderSizeBytes = 0,
                    LastScannedAt = DateTime.UtcNow
                };

                foreach (var tagName in tags)
                {
                    newGame.GameTags.Add(new GameTag { Game = newGame, TagName = tagName });
                }

                db.Games.Add(newGame);
                existingGames[folder] = newGame;
                result.Added++;
            }
        }

        if (folders.Count == 0)
        {
            _logger.Info($"游戏库扫描结束：未找到游戏文件夹。RootPath={rootPath}");
        }

        await db.SaveChangesAsync();
        _logger.Info($"游戏库扫描完成：新增 {result.Added}，更新 {result.Updated}，跳过 {result.Skipped}。RootPath={rootPath}");
        return result;
    }

    public async Task<List<Game>> GetAllGamesAsync()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.Games
            .Include(g => g.GameTags)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.GameTags
            .Select(gt => gt.TagName)
            .Distinct()
            .OrderBy(name => name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Game?> GetGameByIdAsync(int gameId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.Games
            .Include(g => g.GameTags)
            .FirstOrDefaultAsync(g => g.Id == gameId);
    }

    public async Task<Game?> RenameGameAsync(int gameId, string newTitle)
    {
        var title = newTitle.Trim();
        if (string.IsNullOrEmpty(title))
        {
            _logger.Warn($"游戏重命名失败：新标题为空。GameId={gameId}");
            return null;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var game = await db.Games
            .Include(g => g.GameTags)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null)
        {
            _logger.Warn($"游戏重命名失败：记录不存在。GameId={gameId}");
            return null;
        }

        game.Title = title;
        await db.SaveChangesAsync();
        _logger.Info($"游戏记录已重命名：GameId={gameId}，Title={title}");
        return game;
    }

    public async Task AddTagToGameAsync(int gameId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn($"添加游戏标签跳过：标签为空。GameId={gameId}");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var game = await db.Games
            .Include(g => g.GameTags)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null)
        {
            _logger.Warn($"添加游戏标签失败：记录不存在。GameId={gameId}，Tag={name}");
            return;
        }

        if (game.GameTags.Any(gt => gt.TagName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        game.GameTags.Add(new GameTag { Game = game, TagName = name });
        await db.SaveChangesAsync();
        _logger.Info($"游戏标签已添加：GameId={gameId}，Tag={name}");
    }

    public async Task RemoveTagFromGameAsync(int gameId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn($"移除游戏标签跳过：标签为空。GameId={gameId}");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var link = await db.GameTags
            .FirstOrDefaultAsync(gt => gt.GameId == gameId && gt.TagName == name);
        if (link == null)
        {
            _logger.Warn($"移除游戏标签跳过：标签关系不存在。GameId={gameId}，Tag={name}");
            return;
        }

        db.GameTags.Remove(link);
        await db.SaveChangesAsync();
        _logger.Info($"游戏标签已移除：GameId={gameId}，Tag={name}");
    }

    public async Task DeleteTagAsync(string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn("删除游戏标签跳过：标签为空");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var links = await db.GameTags
            .Where(gt => gt.TagName == name)
            .ToListAsync();

        db.GameTags.RemoveRange(links);
        await db.SaveChangesAsync();
        _logger.Info($"游戏标签已删除：Tag={name}, Count={links.Count}");
    }

    public async Task DeleteGameAsync(int gameId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var game = await db.Games
            .Include(g => g.GameTags)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null)
        {
            _logger.Warn($"删除游戏记录跳过：记录不存在。GameId={gameId}");
            return;
        }

        db.GameTags.RemoveRange(game.GameTags);
        db.Games.Remove(game);
        await db.SaveChangesAsync();
        _logger.Info($"游戏记录已删除：GameId={gameId}，Title={game.Title}，FolderPath={game.FolderPath}");
    }

    public async Task<List<string>> GetExecutableCandidatesAsync(int gameId)
    {
        var game = await GetGameByIdAsync(gameId);
        if (game == null || string.IsNullOrWhiteSpace(game.FolderPath) || !Directory.Exists(game.FolderPath))
        {
            return new List<string>();
        }

        return FindExecutableCandidates(game.FolderPath, game.Title);
    }

    public async Task<Game?> SetLaunchPathAsync(int gameId, string launchPath)
    {
        if (string.IsNullOrWhiteSpace(launchPath) || !File.Exists(launchPath))
        {
            _logger.Warn($"设置游戏启动项失败：文件不存在。GameId={gameId}，LaunchPath={launchPath}");
            return null;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var game = await db.Games
            .Include(g => g.GameTags)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null)
        {
            _logger.Warn($"设置游戏启动项失败：记录不存在。GameId={gameId}");
            return null;
        }

        if (!launchPath.StartsWith(game.FolderPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warn($"设置游戏启动项失败：启动项不在游戏目录内。GameId={gameId}，LaunchPath={launchPath}");
            return null;
        }

        game.LaunchPath = launchPath;
        await db.SaveChangesAsync();
        _logger.Info($"游戏启动项已设置：GameId={gameId}，LaunchPath={launchPath}");
        return game;
    }

    public async Task<Game?> SetScreenshotRootPathAsync(int gameId, string screenshotRootPath)
    {
        var path = screenshotRootPath.Trim();
        if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
        {
            _logger.Warn($"设置游戏截图目录失败：目录不存在。GameId={gameId}, ScreenshotRootPath={path}");
            return null;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var game = await db.Games
            .Include(g => g.GameTags)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null)
        {
            _logger.Warn($"设置游戏截图目录失败：记录不存在。GameId={gameId}");
            return null;
        }

        game.ScreenshotRootPath = path;
        await db.SaveChangesAsync();
        _logger.Info($"游戏截图目录已设置：GameId={gameId}, ScreenshotRootPath={path}");
        return game;
    }

    public async Task<List<string>> GetScreenshotImagesAsync(int gameId)
    {
        var game = await GetGameByIdAsync(gameId);
        if (game == null ||
            string.IsNullOrWhiteSpace(game.ScreenshotRootPath) ||
            !Directory.Exists(game.ScreenshotRootPath))
        {
            return new List<string>();
        }

        try
        {
            return Directory.EnumerateFiles(game.ScreenshotRootPath, "*.*", CreateEnumerationOptions(recurse: true))
                .Where(path => ScreenshotExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Warn($"读取游戏截图目录失败。GameId={gameId}, ScreenshotRootPath={game.ScreenshotRootPath}", ex);
            return new List<string>();
        }
    }

    private static List<string> FindGameFolders(string rootPath)
    {
        var gameFolders = new List<string>();

        try
        {
            foreach (var folder in Directory.EnumerateDirectories(rootPath, "*", CreateEnumerationOptions(recurse: false)))
            {
                CollectGameFolders(folder, gameFolders);
            }
        }
        catch
        {
        }

        return gameFolders;
    }

    private static void CollectGameFolders(string folderPath, List<string> gameFolders)
    {
        try
        {
            if (Directory.EnumerateFiles(folderPath, "*", CreateEnumerationOptions(recurse: false)).Any())
            {
                gameFolders.Add(folderPath);
                return;
            }

            foreach (var childFolder in Directory.EnumerateDirectories(folderPath, "*", CreateEnumerationOptions(recurse: false)))
            {
                CollectGameFolders(childFolder, gameFolders);
            }
        }
        catch
        {
            // Ignore folders that cannot be read and keep scanning the rest of the library.
        }
    }

    private Dictionary<string, string> BuildPosterIndex(string? posterRootPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(posterRootPath) || !Directory.Exists(posterRootPath))
        {
            return result;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(posterRootPath, "*.*", CreateEnumerationOptions(recurse: true)))
            {
                if (!PosterExtensions.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(file);
                result.TryAdd(NormalizeKey(name), file);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"扫描游戏海报目录失败。PosterRootPath={posterRootPath}", ex);
        }

        return result;
    }

    private string? ResolvePosterFile(
        string gameFolder,
        string title,
        string? posterRootPath,
        Dictionary<string, string> posterIndex)
    {
        if (posterIndex.TryGetValue(NormalizeKey(title), out var indexedPoster))
        {
            return indexedPoster;
        }

        var localPoster = FindLocalPosterFile(gameFolder);
        if (localPoster == null)
        {
            return null;
        }

        var preservedPoster = PreservePoster(localPoster, title, posterRootPath);
        return preservedPoster ?? localPoster;
    }

    private static string? FindLocalPosterFile(string gameFolder)
    {
        List<string> images;
        try
        {
            images = Directory.EnumerateFiles(gameFolder, "*", CreateEnumerationOptions(recurse: false))
                .Where(f => PosterExtensions.Contains(Path.GetExtension(f)))
                .ToList();
        }
        catch
        {
            return null;
        }

        foreach (var priorityName in PosterNamePriority)
        {
            var match = images.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(priorityName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }

        return images.FirstOrDefault();
    }

    private string? PreservePoster(string localPosterPath, string title, string? posterRootPath)
    {
        if (string.IsNullOrWhiteSpace(posterRootPath))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(posterRootPath);

            var posterRoot = new DirectoryInfo(posterRootPath).FullName;
            var localPoster = new FileInfo(localPosterPath).FullName;
            if (localPoster.StartsWith(posterRoot, StringComparison.OrdinalIgnoreCase))
            {
                return localPoster;
            }

            var extension = Path.GetExtension(localPosterPath);
            var targetPath = Path.Combine(posterRootPath, $"{NormalizeFileName(title)}{extension}");
            if (!File.Exists(targetPath))
            {
                File.Copy(localPosterPath, targetPath);
            }

            return targetPath;
        }
        catch (Exception ex)
        {
            _logger.Warn($"保存游戏海报副本失败。Title={title}, LocalPosterPath={localPosterPath}, PosterRootPath={posterRootPath}", ex);
            return null;
        }
    }

    private static List<string> FindExecutableCandidates(string gameFolder, string title)
    {
        List<string> executables;
        try
        {
            executables = Directory.EnumerateFiles(gameFolder, "*.exe", CreateEnumerationOptions(recurse: true))
                .Where(IsLikelyLaunchExecutable)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }

        return executables
            .OrderBy(path => ScoreExecutable(path, gameFolder, title))
            .ThenBy(path => path.Length)
            .ToList();
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

    private static bool IsLikelyLaunchExecutable(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (ExcludedExecutableNameParts.Any(part => fileName.Contains(part, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !parts.Any(part => ExcludedExecutablePathParts.Any(excluded =>
            part.Equals(excluded, StringComparison.OrdinalIgnoreCase)));
    }

    private static int ScoreExecutable(string path, string gameFolder, string title)
    {
        var score = 0;
        var relative = Path.GetRelativePath(gameFolder, path);
        var depth = relative.Count(ch => ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar);
        score += depth * 10;

        var fileName = NormalizeKey(Path.GetFileNameWithoutExtension(path));
        var titleKey = NormalizeKey(title);
        if (fileName.Equals(titleKey, StringComparison.OrdinalIgnoreCase))
        {
            score -= 100;
        }
        else if (fileName.Contains(titleKey, StringComparison.OrdinalIgnoreCase) ||
                 titleKey.Contains(fileName, StringComparison.OrdinalIgnoreCase))
        {
            score -= 50;
        }

        return score;
    }

    private static List<string> ExtractTagsFromPath(string root, string folder)
    {
        var rootPath = new DirectoryInfo(root).FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folderPath = new DirectoryInfo(folder).FullName;
        var relative = folderPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
            ? folderPath.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : Path.GetFileName(folder);

        var parts = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 1
            ? parts.Take(parts.Length - 1).ToList()
            : new List<string>();
    }

    private static void SyncTags(Game game, List<string> tagNames)
    {
        var currentTagNames = game.GameTags
            .Select(gt => gt.TagName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tagName in tagNames)
        {
            if (currentTagNames.Contains(tagName))
            {
                continue;
            }

            game.GameTags.Add(new GameTag { Game = game, TagName = tagName });
        }
    }

    private static string NormalizeKey(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string NormalizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var fileName = new string(value
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(fileName) ? "game" : fileName;
    }
}
