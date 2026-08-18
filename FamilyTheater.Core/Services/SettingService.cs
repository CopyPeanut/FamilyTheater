using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class SettingService : ISettingService
{
    private const string MediaRootPathKey = "MediaRootPath";
    private const string MoviePosterRootPathKey = "MoviePosterRootPath";
    private const string PictureRootPathKey = "PictureRootPath";
    private const string GameRootPathKey = "GameRootPath";
    private const string GamePosterRootPathKey = "GamePosterRootPath";

    private readonly ILibraryDbContextFactory _dbContextFactory;
    private readonly IAppLogger _logger;

    public SettingService(ILibraryDbContextFactory dbContextFactory, IAppLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var setting = await db.Settings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => new { s.Value })
            .FirstOrDefaultAsync();

        return setting?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            db.Settings.Add(new Setting { Key = key, Value = value });
        }

        await db.SaveChangesAsync();
        _logger.Info($"设置已保存：{key}={value}，Db={_dbContextFactory.CurrentDatabasePath}");
    }

    public Task<string?> GetMediaRootPathAsync() => GetAsync(MediaRootPathKey);

    public Task SetMediaRootPathAsync(string path) => SetAsync(MediaRootPathKey, path);

    public Task<string?> GetMoviePosterRootPathAsync() => GetAsync(MoviePosterRootPathKey);

    public Task SetMoviePosterRootPathAsync(string path) => SetAsync(MoviePosterRootPathKey, path);

    public Task<string?> GetGameRootPathAsync() => GetAsync(GameRootPathKey);

    public Task SetGameRootPathAsync(string path) => SetAsync(GameRootPathKey, path);

    public Task<string?> GetGamePosterRootPathAsync() => GetAsync(GamePosterRootPathKey);

    public Task SetGamePosterRootPathAsync(string path) => SetAsync(GamePosterRootPathKey, path);

    public Task<string?> GetPictureRootPathAsync() => GetAsync(PictureRootPathKey);

    public Task SetPictureRootPathAsync(string path) => SetAsync(PictureRootPathKey, path);
}
