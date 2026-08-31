using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LoginWindow.Models
{
    public class ConfigWindowModel : ReactiveObject
    {
        private readonly ISettingService _settingService;
        private readonly IMovieService _movieService;
        private readonly IPictureService _pictureService;
        private readonly IGameService _gameService;
        private readonly IMangaService _mangaService;
        private readonly IAppLogger _logger;

        [Reactive] public string MediaRootPath { get; set; } = string.Empty;
        [Reactive] public string MoviePosterRootPath { get; set; } = string.Empty;
        [Reactive] public string PictureRootPath { get; set; } = string.Empty;
        [Reactive] public string GameRootPath { get; set; } = string.Empty;
        [Reactive] public string GamePosterRootPath { get; set; } = string.Empty;
        [Reactive] public string MangaRootPath { get; set; } = string.Empty;
        [Reactive] public string MangaPosterRootPath { get; set; } = string.Empty;
        [Reactive] public string StatusMessage { get; set; } = string.Empty;
        [Reactive] public bool IsScanning { get; set; }

        public ReactiveCommand<string, Unit> BrowseCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> FullMovieScanCommand { get; }

        public ConfigWindowModel(
            ISettingService settingService,
            IMovieService movieService,
            IPictureService pictureService,
            IGameService gameService,
            IMangaService mangaService,
            IAppLogger logger)
        {
            _settingService = settingService;
            _movieService = movieService;
            _pictureService = pictureService;
            _gameService = gameService;
            _mangaService = mangaService;
            _logger = logger;

            var canSave = this.WhenAnyValue(x => x.IsScanning, scanning => !scanning);
            SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
            FullMovieScanCommand = ReactiveCommand.CreateFromTask(FullMovieScanAsync, canSave);
            BrowseCommand = ReactiveCommand.CreateFromTask<string>(BrowseAsync, canSave);

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                MediaRootPath = await _settingService.GetMediaRootPathAsync() ?? string.Empty;
                MoviePosterRootPath = await _settingService.GetMoviePosterRootPathAsync() ?? string.Empty;
                PictureRootPath = await _settingService.GetPictureRootPathAsync() ?? string.Empty;
                GameRootPath = await _settingService.GetGameRootPathAsync() ?? string.Empty;
                GamePosterRootPath = await _settingService.GetGamePosterRootPathAsync() ?? string.Empty;
                MangaRootPath = await _settingService.GetMangaRootPathAsync() ?? string.Empty;
                MangaPosterRootPath = await _settingService.GetMangaPosterRootPathAsync() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.Error("加载配置失败。", ex);
                StatusMessage = $"加载配置失败：{ex.Message}";
            }
        }

        private Task BrowseAsync(string target)
        {
            var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                Description = target switch
                {
                    "picture" => "选择图片根目录",
                    "poster" => "选择电影海报根目录",
                    "game" => "选择游戏根目录",
                    "gamePoster" => "选择游戏海报根目录",
                    "manga" => "选择漫画根目录",
                    "mangaPoster" => "选择漫画封面根目录",
                    _ => "选择媒体根目录"
                }
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                switch (target)
                {
                    case "picture":
                        PictureRootPath = dialog.SelectedPath;
                        break;
                    case "poster":
                        MoviePosterRootPath = dialog.SelectedPath;
                        break;
                    case "game":
                        GameRootPath = dialog.SelectedPath;
                        break;
                    case "gamePoster":
                        GamePosterRootPath = dialog.SelectedPath;
                        break;
                    case "manga":
                        MangaRootPath = dialog.SelectedPath;
                        break;
                    case "mangaPoster":
                        MangaPosterRootPath = dialog.SelectedPath;
                        break;
                    default:
                        MediaRootPath = dialog.SelectedPath;
                        break;
                }
            }

            return Task.CompletedTask;
        }

        private async Task SaveAsync()
        {
            IsScanning = true;
            StatusMessage = "正在保存并扫描媒体库...";

            try
            {
                await SaveSettingsAsync();
                var movieResult = await _movieService.ScanLibraryAsync();
                var mediaMessage = movieResult.Added == 0 && movieResult.Updated == 0 && movieResult.Skipped == 0
                    ? "电影：未发现可入库的视频文件"
                    : $"电影：新增 {movieResult.Added} 部，更新 {movieResult.Updated} 部，跳过 {movieResult.Skipped} 部已有视频";

                var pictureResult = await _pictureService.ScanLibraryAsync();
                var pictureMessage = pictureResult.Added == 0 && pictureResult.Updated == 0 && pictureResult.Skipped == 0
                    ? "图片：未发现可入库的图片文件"
                    : $"图片：新增 {pictureResult.Added} 张，更新 {pictureResult.Updated} 张，跳过 {pictureResult.Skipped} 个文件夹";

                var mangaResult = await _mangaService.ScanLibraryAsync();
                var mangaMessage = mangaResult.Added == 0 && mangaResult.Updated == 0 && mangaResult.Skipped == 0
                    ? "漫画：未发现可入库的 PDF 文件"
                    : $"漫画：新增 {mangaResult.Added} 本，更新 {mangaResult.Updated} 本，跳过 {mangaResult.Skipped} 个文件夹";

                var gameResult = await _gameService.ScanLibraryAsync();
                var gameMessage = gameResult.Added == 0 && gameResult.Updated == 0 && gameResult.Skipped == 0
                    ? "游戏：未发现可入库的游戏文件夹"
                    : $"游戏：新增 {gameResult.Added} 个，更新 {gameResult.Updated} 个，跳过 {gameResult.Skipped} 个文件夹";

                StatusMessage = $"{mediaMessage}；{pictureMessage}；{mangaMessage}；{gameMessage}";
            }
            catch (Exception ex)
            {
                _logger.Error("保存配置并扫描媒体库失败。", ex);
                StatusMessage = $"保存失败：{ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async Task FullMovieScanAsync()
        {
            IsScanning = true;
            StatusMessage = "正在保存并完整重新扫描电影库...";

            try
            {
                await SaveSettingsAsync();
                var result = await _movieService.ScanLibraryAsync(fullRescan: true);
                StatusMessage = result.Added == 0 && result.Updated == 0 && result.Skipped == 0
                    ? "电影完整扫描完成：未发现可入库的视频文件"
                    : $"电影完整扫描完成：新增 {result.Added} 部，更新 {result.Updated} 部，跳过 {result.Skipped} 部";
            }
            catch (Exception ex)
            {
                _logger.Error("保存配置并完整扫描电影库失败。", ex);
                StatusMessage = $"电影完整扫描失败：{ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            await _settingService.SetMediaRootPathAsync(MediaRootPath ?? string.Empty);
            await _settingService.SetMoviePosterRootPathAsync(MoviePosterRootPath ?? string.Empty);
            await _settingService.SetPictureRootPathAsync(PictureRootPath ?? string.Empty);
            await _settingService.SetMangaRootPathAsync(MangaRootPath ?? string.Empty);
            await _settingService.SetMangaPosterRootPathAsync(MangaPosterRootPath ?? string.Empty);
            await _settingService.SetGameRootPathAsync(GameRootPath ?? string.Empty);
            await _settingService.SetGamePosterRootPathAsync(GamePosterRootPath ?? string.Empty);
        }
    }
}
