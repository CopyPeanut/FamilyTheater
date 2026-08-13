using FamilyTheater.Core.Logger;

namespace FamilyTheater.Core.Helper;

public static class FFmpegHelper
{
    private static readonly string BinDir = Path.Combine(AppContext.BaseDirectory, "ffmpeg");
    private static readonly string FfmpegPath = Path.Combine(BinDir, "ffmpeg.exe");
    private static readonly object SyncRoot = new();

    private static bool _configured;
    private static bool _available;

    public static string FfmpegExePath => FfmpegPath;

    public static Task<bool> EnsureAvailableAsync(IAppLogger? logger = null)
    {
        lock (SyncRoot)
        {
            if (_configured)
                return Task.FromResult(_available);

            _configured = true;
        }

        try
        {
            if (File.Exists(FfmpegPath))
            {
                FFMpegCore.GlobalFFOptions.Configure(new FFMpegCore.FFOptions
                {
                    BinaryFolder = BinDir
                });

                _available = true;
                logger?.Info($"FFmpeg 已就绪：{FfmpegPath}");
                return Task.FromResult(true);
            }

            if (TryFindInPath("ffmpeg.exe", logger))
            {
                FFMpegCore.GlobalFFOptions.Configure(new FFMpegCore.FFOptions());
                _available = true;
                logger?.Info("FFmpeg 已从系统 PATH 中找到。");
                return Task.FromResult(true);
            }

            _available = false;
            logger?.Warn($"未找到 FFmpeg：{FfmpegPath}");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _available = false;
            logger?.Error("检查 FFmpeg 可用性失败。", ex);
            return Task.FromResult(false);
        }
    }

    public static bool IsAvailable =>
        _available && (File.Exists(FfmpegPath) || TryFindInPath("ffmpeg.exe"));

    private static bool TryFindInPath(string exeName, IAppLogger? logger = null)
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return false;

            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(dir))
                    continue;

                var full = Path.Combine(dir, exeName);
                if (File.Exists(full))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger?.Warn($"检查 PATH 中的 {exeName} 失败。", ex);
            return false;
        }
    }
}
