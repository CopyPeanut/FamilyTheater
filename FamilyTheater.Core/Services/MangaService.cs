using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;
using Windows.Data.Pdf;
using Windows.Storage;

namespace FamilyTheater.Core.Services;

public class MangaService : IMangaService
{
    private static readonly HashSet<string> MangaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    private static readonly HashSet<string> PosterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp"
    };

    private static readonly string[] PosterNamePriority = { "poster", "cover", "folder" };
    private const string LegacyAutoPosterSuffix = "_cover_auto";

    private readonly ILibraryDbContextFactory _dbContextFactory;
    private readonly ISettingService _settingService;
    private readonly IAppLogger _logger;

    public MangaService(ILibraryDbContextFactory dbContextFactory, ISettingService settingService, IAppLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _settingService = settingService;
        _logger = logger;
    }

    public async Task<ScanResult> ScanLibraryAsync(bool fullRescan = false)
    {
        var result = new ScanResult();
        var rootPath = await _settingService.GetMangaRootPathAsync();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            _logger.Warn($"Manga library scan skipped: invalid root path. RootPath={rootPath}");
            return result;
        }

        var posterRootPath = await _settingService.GetMangaPosterRootPathAsync();
        Dictionary<string, string>? posterIndex = null;

        using var db = _dbContextFactory.CreateDbContext();
        var existingMangas = fullRescan
            ? await db.Mangas
                .Include(m => m.MangaTags)
                .ToDictionaryAsync(m => m.FilePath, m => m, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Manga>(StringComparer.OrdinalIgnoreCase);
        var existingMangaPaths = fullRescan
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                await db.Mangas
                    .Select(m => m.FilePath)
                    .ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

        var discoveredFiles = 0;
        foreach (var mangaFile in EnumerateMangaFilesRecursive(rootPath, result))
        {
            discoveredFiles++;

            var folder = Path.GetDirectoryName(mangaFile) ?? rootPath;
            var title = Path.GetFileNameWithoutExtension(mangaFile);
            if (!fullRescan && existingMangaPaths.Contains(mangaFile))
            {
                result.Skipped++;
                continue;
            }

            var posterFolder = GetPosterFolder(rootPath, posterRootPath, folder);
            var posterPath = FindPosterFileForManga(mangaFile, posterIndex ??= BuildPosterIndex(posterRootPath), posterFolder);
            if (posterPath == null && posterFolder != null)
            {
                posterPath = await ExtractPosterFromPdfAsync(mangaFile, posterFolder, title);
            }

            var tags = ExtractTagsFromPath(rootPath, folder);

            if (existingMangas.TryGetValue(mangaFile, out var manga))
            {
                if (string.IsNullOrWhiteSpace(manga.Title))
                {
                    manga.Title = title;
                }

                manga.FilePath = mangaFile;
                manga.FolderPath = folder;
                manga.PosterPath = SelectPosterPath(manga.PosterPath, posterPath, posterRootPath);
                manga.FileSizeBytes = GetFileSizeBytes(mangaFile);
                manga.LastScannedAt = DateTime.UtcNow;
                SyncTags(manga, tags);
                result.Updated++;
            }
            else
            {
                var newManga = new Manga
                {
                    Title = title,
                    FilePath = mangaFile,
                    FolderPath = folder,
                    PosterPath = posterPath,
                    FileSizeBytes = GetFileSizeBytes(mangaFile),
                    LastScannedAt = DateTime.UtcNow
                };

                foreach (var tagName in tags)
                {
                    newManga.MangaTags.Add(new MangaTag { Manga = newManga, TagName = tagName });
                }

                db.Mangas.Add(newManga);
                existingMangas[mangaFile] = newManga;
                existingMangaPaths.Add(mangaFile);
                result.Added++;
            }
        }

        if (discoveredFiles == 0)
        {
            _logger.Info($"Manga library scan finished: no PDF files found. RootPath={rootPath}");
            return result;
        }

        await db.SaveChangesAsync();
        _logger.Info($"Manga library scan finished: added {result.Added}, updated {result.Updated}, skipped {result.Skipped}. RootPath={rootPath}");
        return result;
    }

    public async Task<List<Manga>> GetAllMangasAsync()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.Mangas
            .Include(m => m.MangaTags)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<string>> GetAllTagsAsync()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.MangaTags
            .Select(mt => mt.TagName)
            .Distinct()
            .OrderBy(name => name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Manga?> GetMangaByIdAsync(int mangaId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        return await db.Mangas
            .Include(m => m.MangaTags)
            .FirstOrDefaultAsync(m => m.Id == mangaId);
    }

    public async Task<Manga?> RenameMangaAsync(int mangaId, string newTitle)
    {
        var title = newTitle.Trim();
        if (string.IsNullOrEmpty(title))
        {
            _logger.Warn($"Manga rename failed: empty title. MangaId={mangaId}");
            return null;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var manga = await db.Mangas
            .Include(m => m.MangaTags)
            .FirstOrDefaultAsync(m => m.Id == mangaId);
        if (manga == null)
        {
            _logger.Warn($"Manga rename failed: record not found. MangaId={mangaId}");
            return null;
        }

        if (manga.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
        {
            return manga;
        }

        var folder = Path.GetDirectoryName(manga.FilePath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            _logger.Warn($"Manga rename failed: missing folder. MangaId={mangaId}, FilePath={manga.FilePath}");
            return null;
        }

        var newFilePath = Path.Combine(folder, title + Path.GetExtension(manga.FilePath));
        if (File.Exists(newFilePath) &&
            !string.Equals(newFilePath, manga.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warn($"Manga rename failed: target file exists. MangaId={mangaId}, TargetPath={newFilePath}");
            return null;
        }

        var newPosterPath = GetRenamedPosterPath(manga.PosterPath, title);
        if (!string.IsNullOrEmpty(newPosterPath) &&
            File.Exists(newPosterPath) &&
            !string.Equals(newPosterPath, manga.PosterPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warn($"Manga rename failed: target poster file exists. MangaId={mangaId}, TargetPath={newPosterPath}");
            return null;
        }

        File.Move(manga.FilePath, newFilePath);

        if (!string.IsNullOrEmpty(newPosterPath) &&
            !string.Equals(newPosterPath, manga.PosterPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(manga.PosterPath!, newPosterPath);
            manga.PosterPath = newPosterPath;
        }

        manga.Title = title;
        manga.FilePath = newFilePath;
        manga.FolderPath = folder;
        manga.LastScannedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        _logger.Info($"Manga record renamed. MangaId={mangaId}, Title={title}");
        return manga;
    }

    public async Task<Manga?> SetPosterPathAsync(int mangaId, string posterPath)
    {
        var path = posterPath.Trim();
        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path) ||
            !PosterExtensions.Contains(Path.GetExtension(path)))
        {
            _logger.Warn($"Set manga poster failed: invalid poster file. MangaId={mangaId}, PosterPath={posterPath}");
            return null;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var manga = await db.Mangas
            .Include(m => m.MangaTags)
            .FirstOrDefaultAsync(m => m.Id == mangaId);
        if (manga == null)
        {
            _logger.Warn($"Set manga poster failed: record not found. MangaId={mangaId}, PosterPath={path}");
            return null;
        }

        manga.PosterPath = path;
        manga.LastScannedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        _logger.Info($"Manga poster set. MangaId={mangaId}, PosterPath={path}");
        return manga;
    }

    public async Task AddTagToMangaAsync(int mangaId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn($"Add manga tag skipped: empty tag. MangaId={mangaId}");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var manga = await db.Mangas
            .Include(m => m.MangaTags)
            .FirstOrDefaultAsync(m => m.Id == mangaId);
        if (manga == null)
        {
            _logger.Warn($"Add manga tag failed: record not found. MangaId={mangaId}, Tag={name}");
            return;
        }

        if (manga.MangaTags.Any(mt => mt.TagName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        manga.MangaTags.Add(new MangaTag { Manga = manga, TagName = name });
        await db.SaveChangesAsync();
        _logger.Info($"Manga tag added. MangaId={mangaId}, Tag={name}");
    }

    public async Task RemoveTagFromMangaAsync(int mangaId, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn($"Remove manga tag skipped: empty tag. MangaId={mangaId}");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var link = await db.MangaTags
            .FirstOrDefaultAsync(mt => mt.MangaId == mangaId && mt.TagName == name);
        if (link == null)
        {
            _logger.Warn($"Remove manga tag skipped: link not found. MangaId={mangaId}, Tag={name}");
            return;
        }

        db.MangaTags.Remove(link);
        await db.SaveChangesAsync();
        _logger.Info($"Manga tag removed. MangaId={mangaId}, Tag={name}");
    }

    public async Task DeleteTagAsync(string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _logger.Warn("Delete manga tag skipped: empty tag.");
            return;
        }

        using var db = _dbContextFactory.CreateDbContext();
        var links = await db.MangaTags
            .Where(mt => mt.TagName == name)
            .ToListAsync();

        db.MangaTags.RemoveRange(links);
        await db.SaveChangesAsync();
        _logger.Info($"Manga tag deleted. Tag={name}, Count={links.Count}");
    }

    public async Task DeleteMangaAsync(int mangaId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var manga = await db.Mangas
            .Include(m => m.MangaTags)
            .FirstOrDefaultAsync(m => m.Id == mangaId);
        if (manga == null)
        {
            _logger.Warn($"Delete manga skipped: record not found. MangaId={mangaId}");
            return;
        }

        db.MangaTags.RemoveRange(manga.MangaTags);
        db.Mangas.Remove(manga);
        await db.SaveChangesAsync();
        _logger.Info($"Manga record deleted. MangaId={mangaId}, Title={manga.Title}, FilePath={manga.FilePath}");
    }

    private IEnumerable<string> EnumerateMangaFilesRecursive(string rootPath, ScanResult result)
    {
        using var enumerator = CreateRecursiveFileEnumerator(rootPath, result);
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
                _logger.Warn($"Unable to read manga folder. RootPath={rootPath}", ex);
                yield break;
            }
            catch (DirectoryNotFoundException ex)
            {
                result.Skipped++;
                _logger.Warn($"Manga folder does not exist. RootPath={rootPath}", ex);
                yield break;
            }

            if (MangaExtensions.Contains(Path.GetExtension(file)))
            {
                yield return file;
            }
        }
    }

    private IEnumerator<string>? CreateRecursiveFileEnumerator(string rootPath, ScanResult result)
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
            _logger.Warn($"Unable to access manga root folder: {rootPath}", ex);
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            result.Skipped++;
            _logger.Warn($"Manga root folder does not exist: {rootPath}", ex);
            return null;
        }
    }

    private long GetFileSizeBytes(string filePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Warn($"Unable to read manga file size: {filePath}", ex);
            return 0;
        }
        catch (FileNotFoundException ex)
        {
            _logger.Warn($"Manga file missing, unable to read size: {filePath}", ex);
            return 0;
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
            _logger.Warn($"Manga poster root path is invalid, skipping independent poster scan. PosterRootPath={posterRootPath}");
            return index;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(posterRootPath, "*", CreateEnumerationOptions(recurse: true)))
            {
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
        catch (Exception ex)
        {
            _logger.Warn($"Scan manga poster folder failed. PosterRootPath={posterRootPath}", ex);
        }

        return index;
    }

    private static void AddPosterCandidate(Dictionary<string, string> index, string? key, string file)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        index.TryAdd(key, file);
    }

    private string? FindPosterFileForManga(
        string mangaFile,
        Dictionary<string, string> posterIndex,
        string? autoPosterFolder)
    {
        var title = Path.GetFileNameWithoutExtension(mangaFile);
        if (posterIndex.TryGetValue(title, out var indexedPoster))
        {
            return indexedPoster;
        }

        if (string.IsNullOrEmpty(autoPosterFolder))
        {
            return null;
        }

        var autoPosterPath = Path.Combine(autoPosterFolder, $"{GetSafeFileName(title)}.png");
        return File.Exists(autoPosterPath) ? autoPosterPath : null;
    }

    private async Task<string?> ExtractPosterFromPdfAsync(string pdfPath, string folder, string title)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var posterPath = Path.Combine(folder, $"{GetSafeFileName(title)}.png");
            if (File.Exists(posterPath))
            {
                return posterPath;
            }

            var pdfFile = await StorageFile.GetFileFromPathAsync(pdfPath);
            var document = await PdfDocument.LoadFromFileAsync(pdfFile);
            if (document.PageCount == 0)
            {
                _logger.Warn($"Manga poster extraction skipped: PDF has no pages. PdfPath={pdfPath}");
                return null;
            }

            using var page = document.GetPage(0);
            var targetFolder = await StorageFolder.GetFolderFromPathAsync(folder);
            var targetFile = await targetFolder.CreateFileAsync(Path.GetFileName(posterPath), CreationCollisionOption.FailIfExists);
            using var stream = await targetFile.OpenAsync(FileAccessMode.ReadWrite);
            var options = new PdfPageRenderOptions
            {
                DestinationWidth = 1284
            };

            await page.RenderToStreamAsync(stream, options);
            return File.Exists(posterPath) ? posterPath : null;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Extract manga poster from PDF failed. PdfPath={pdfPath}", ex);
            return null;
        }
    }

    private static string? GetPosterFolder(string mangaRootPath, string? posterRootPath, string mangaFolder)
    {
        if (string.IsNullOrWhiteSpace(posterRootPath))
        {
            return null;
        }

        var relative = Path.GetRelativePath(mangaRootPath, mangaFolder);
        if (relative == "." || relative.StartsWith(".."))
        {
            relative = string.Empty;
        }

        return Path.Combine(posterRootPath, relative);
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

        return Path.Combine(folder, $"{GetSafeFileName(title)}{extension}");
    }

    private static bool IsAutoGeneratedPosterFile(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return false;
        }

        return Path.GetFileNameWithoutExtension(file)
            .EndsWith(LegacyAutoPosterSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldKeepExistingPoster(string? posterPath)
    {
        return !string.IsNullOrWhiteSpace(posterPath) &&
               File.Exists(posterPath) &&
               PosterExtensions.Contains(Path.GetExtension(posterPath));
    }

    private static string? SelectPosterPath(
        string? existingPosterPath,
        string? scannedPosterPath,
        string? posterRootPath)
    {
        if (ShouldKeepExistingPoster(existingPosterPath) &&
            IsPathUnderRoot(existingPosterPath, posterRootPath) &&
            !IsAutoGeneratedPosterFile(existingPosterPath))
        {
            return existingPosterPath;
        }

        return scannedPosterPath;
    }

    private static bool IsPathUnderRoot(string? path, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var fullRootPath = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return fullPath.Equals(fullRootPath, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(
                   fullRootPath + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(
                   fullRootPath + Path.AltDirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
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
        var rootPath = new DirectoryInfo(root).FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folderPath = new DirectoryInfo(folder).FullName;
        var relative = folderPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
            ? folderPath.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : Path.GetFileName(folder);

        var parts = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        return parts.ToList();
    }

    private static void SyncTags(Manga manga, List<string> tagNames)
    {
        var currentTagNames = manga.MangaTags
            .Select(mt => mt.TagName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tagName in tagNames)
        {
            if (currentTagNames.Contains(tagName))
            {
                continue;
            }

            manga.MangaTags.Add(new MangaTag { Manga = manga, TagName = tagName });
        }
    }
}
