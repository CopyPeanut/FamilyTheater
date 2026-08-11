using System.Threading.Tasks;

namespace FamilyTheater.Core.Services
{
    public interface ISettingService
    {
        /// <summary>
        /// 读取指定 key 的配置值，不存在返回 null。
        /// </summary>
        Task<string?> GetAsync(string key);

        /// <summary>
        /// 写入/更新指定 key 的配置值。
        /// </summary>
        Task SetAsync(string key, string value);

        /// <summary>
        /// 读取媒体根目录配置（key = "MediaRootPath"）。
        /// </summary>
        Task<string?> GetMediaRootPathAsync();

        /// <summary>
        /// 写入媒体根目录配置。
        /// </summary>
        Task SetMediaRootPathAsync(string path);

        /// <summary>
        /// 读取图片根目录配置（key = "PictureRootPath"）。
        /// </summary>
        Task<string?> GetPictureRootPathAsync();

        /// <summary>
        /// 写入图片根目录配置。
        /// </summary>
        Task SetPictureRootPathAsync(string path);
    }
}