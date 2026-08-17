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
        private readonly IAppLogger _logger;

        [Reactive] public string MediaRootPath { get; set; } = string.Empty;
        [Reactive] public string MoviePosterRootPath { get; set; } = string.Empty;
        [Reactive] public string PictureRootPath { get; set; } = string.Empty;
        [Reactive] public string StatusMessage { get; set; } = string.Empty;
        [Reactive] public bool IsScanning { get; set; }

        public ReactiveCommand<string, Unit> BrowseCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public ConfigWindowModel(
            ISettingService settingService,
            IMovieService movieService,
            IPictureService pictureService,
            IAppLogger logger)
        {
            _settingService = settingService;
            _movieService = movieService;
            _pictureService = pictureService;
            _logger = logger;

            var canSave = this.WhenAnyValue(x => x.IsScanning, scanning => !scanning);
            SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
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
                    _ => "选择媒体根目录"
                }
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (target == "picture")
                {
                    PictureRootPath = dialog.SelectedPath;
                }
                else if (target == "poster")
                {
                    MoviePosterRootPath = dialog.SelectedPath;
                }
                else
                {
                    MediaRootPath = dialog.SelectedPath;
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
                await _settingService.SetMediaRootPathAsync(MediaRootPath ?? string.Empty);
                await _settingService.SetMoviePosterRootPathAsync(MoviePosterRootPath ?? string.Empty);
                var movieResult = await _movieService.ScanLibraryAsync();
                var mediaMessage = movieResult.Added == 0 && movieResult.Updated == 0 && movieResult.Skipped == 0
                    ? "电影：未发现可入库的视频文件"
                    : $"电影：新增 {movieResult.Added} 部，更新 {movieResult.Updated} 部，跳过 {movieResult.Skipped} 个文件夹";

                await _settingService.SetPictureRootPathAsync(PictureRootPath ?? string.Empty);
                var pictureResult = await _pictureService.ScanLibraryAsync();
                var pictureMessage = pictureResult.Added == 0 && pictureResult.Updated == 0 && pictureResult.Skipped == 0
                    ? "图片：未发现可入库的图片文件"
                    : $"图片：新增 {pictureResult.Added} 张，更新 {pictureResult.Updated} 张，跳过 {pictureResult.Skipped} 个文件夹";

                StatusMessage = $"{mediaMessage}；{pictureMessage}";
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
    }
}
