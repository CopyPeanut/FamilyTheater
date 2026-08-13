using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class SettingService : ISettingService
{
    private const string MediaRootPathKey = "MediaRootPath";
    private const string PictureRootPathKey = "PictureRootPath";

    private readonly AppDbContext _db;
    private readonly IAppLogger _logger;

    public SettingService(AppDbContext db, IAppLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _db.Settings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => new { s.Value })
            .FirstOrDefaultAsync();

        return setting?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        var existing = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            _db.Settings.Add(new Setting { Key = key, Value = value });
        }

        await _db.SaveChangesAsync();
        _logger.Info($"设置已保存：{key}={value}");
    }

    public Task<string?> GetMediaRootPathAsync() => GetAsync(MediaRootPathKey);

    public Task SetMediaRootPathAsync(string path) => SetAsync(MediaRootPathKey, path);

    public Task<string?> GetPictureRootPathAsync() => GetAsync(PictureRootPathKey);

    public Task SetPictureRootPathAsync(string path) => SetAsync(PictureRootPathKey, path);
}
