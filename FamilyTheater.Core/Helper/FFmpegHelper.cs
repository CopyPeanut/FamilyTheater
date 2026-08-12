using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CoreLogger = FamilyTheater.Core.Logger.Logger;

namespace FamilyTheater.Core.Helper
{
    /// <summary>
    /// FFmpeg 二进制文件管理：直接使用项目自带的 ffmpeg.exe/ffprobe.exe，无需下载。
    /// </summary>
    public static class FFmpegHelper
    {
        private static readonly string BinDir = Path.Combine(
            AppContext.BaseDirectory, "ffmpeg");

        private static readonly string FfmpegPath = Path.Combine(BinDir, "ffmpeg.exe");
        public static string FfmpegExePath => FfmpegPath;
        private static bool _configured = false;
        private static readonly object _lock = new();
        private static bool _available = false;

        /// <summary>
        /// 确保 ffmpeg.exe 存在并配置 FFMpegCore 全局路径。线程安全，只需调用一次。
        /// 返回 true 表示 ffmpeg 可用。
        /// </summary>
        public static Task<bool> EnsureAvailableAsync()
        {
            lock (_lock)
            {
                if (_configured)
                    return Task.FromResult(_available);
                _configured = true;
            }

            try
            {
                // 优先用项目自带的 ffmpeg
                if (File.Exists(FfmpegPath))
                {
                    FFMpegCore.GlobalFFOptions.Configure(new FFMpegCore.FFOptions
                    {
                        BinaryFolder = BinDir
                    });
                    _available = true;
                    CoreLogger.Info($"FFmpeg 已就绪：{FfmpegPath}");
                    return Task.FromResult(true);
                }

                // 退而求其次：检查系统 PATH
                if (TryFindInPath("ffmpeg.exe"))
                {
                    FFMpegCore.GlobalFFOptions.Configure(new FFMpegCore.FFOptions());
                    _available = true;
                    CoreLogger.Info("FFmpeg 已从系统 PATH 中找到。");
                    return Task.FromResult(true);
                }

                _available = false;
                CoreLogger.Warn($"未找到 FFmpeg：{FfmpegPath}");
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _available = false;
                CoreLogger.Error("检查 FFmpeg 可用性失败。", ex);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// ffmpeg 是否已就绪（非阻塞检查）。
        /// </summary>
        public static bool IsAvailable =>
            _available && (File.Exists(FfmpegPath) || TryFindInPath("ffmpeg.exe"));

        private static bool TryFindInPath(string exeName)
        {
            try
            {
                var path = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrEmpty(path)) return false;
                foreach (var dir in path.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    var full = Path.Combine(dir, exeName);
                    if (File.Exists(full)) return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                CoreLogger.Warn($"检查 PATH 中的 {exeName} 失败。", ex);
                return false;
            }
        }
    }
}
