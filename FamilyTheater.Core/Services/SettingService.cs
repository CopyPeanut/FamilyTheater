using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Services
{
    public class SettingService : ISettingService
    {
        private readonly AppDbContext _db;
        private const string MediaRootPathKey = "MediaRootPath";

        public SettingService(AppDbContext db)
        {
            _db = db;
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
        }

        public Task<string?> GetMediaRootPathAsync() => GetAsync(MediaRootPathKey);

        public Task SetMediaRootPathAsync(string path) => SetAsync(MediaRootPathKey, path);

        private const string PictureRootPathKey = "PictureRootPath";
        public Task<string?> GetPictureRootPathAsync() => GetAsync(PictureRootPathKey);
        public Task SetPictureRootPathAsync(string path) => SetAsync(PictureRootPathKey, path);
    }
}