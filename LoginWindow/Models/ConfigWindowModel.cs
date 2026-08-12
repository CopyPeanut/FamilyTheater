using FamilyTheater.Core.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Forms;
using CoreLogger = FamilyTheater.Core.Logger.Logger;

namespace LoginWindow.Models
{
    public class ConfigWindowModel : ReactiveObject
    {
        private readonly ISettingService _settingService;
        private readonly IMovieService _movieService;
        private readonly IPictureService _pictureService;

        [Reactive] public string MediaRootPath { get; set; } = string.Empty;
        [Reactive] public string PictureRootPath { get; set; } = string.Empty;
        [Reactive] public string StatusMessage { get; set; } = string.Empty;
        [Reactive] public bool IsScanning { get; set; }

        public ReactiveCommand<string, Unit> BrowseCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public ConfigWindowModel(ISettingService settingService, IMovieService movieService, IPictureService pictureService)
        {
            _settingService = settingService;
            _movieService = movieService;
            _pictureService = pictureService;

            // 保存命令在扫描期间禁用
            var canSave = this.WhenAnyValue(x => x.IsScanning, scanning => !scanning);
            SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
            BrowseCommand = ReactiveCommand.CreateFromTask<string>(BrowseAsync, canSave);

            // 加载已有配置
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var mediaPath = await _settingService.GetMediaRootPathAsync();
            MediaRootPath = mediaPath ?? string.Empty;

            var picturePath = await _settingService.GetPictureRootPathAsync();
            PictureRootPath = picturePath ?? string.Empty;
        }

        /// <summary>
        /// 浏览选择目录。CommandParameter: "media" 或 "picture"。
        /// </summary>
        private async Task BrowseAsync(string target)
        {
            var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = true
            };

            if (target == "picture")
            {
                dialog.Description = "选择图片根目录";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    PictureRootPath = dialog.SelectedPath;
                }
            }
            else
            {
                dialog.Description = "选择媒体根目录";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    MediaRootPath = dialog.SelectedPath;
                }
            }
        }

        private async Task SaveAsync()
        {
            IsScanning = true;
            StatusMessage = "正在保存并扫描入库...";
            try
            {
                var mediaMsg = string.Empty;
                var pictureMsg = string.Empty;

                // 1. 保存媒体根目录并扫描
                await _settingService.SetMediaRootPathAsync(MediaRootPath ?? string.Empty);
                var movieResult = await _movieService.ScanLibraryAsync();
                mediaMsg = movieResult.Added == 0 && movieResult.Updated == 0 && movieResult.Skipped == 0
                    ? "电影：未发现可入库的视频文件"
                    : $"电影 — 新增 {movieResult.Added} 部，更新 {movieResult.Updated} 部，跳过 {movieResult.Skipped} 个文件夹";

                // 2. 保存图片根目录并扫描
                await _settingService.SetPictureRootPathAsync(PictureRootPath ?? string.Empty);
                var picResult = await _pictureService.ScanLibraryAsync();
                pictureMsg = picResult.Added == 0 && picResult.Updated == 0 && picResult.Skipped == 0
                    ? "图片：未发现可入库的图片文件"
                    : $"图片 — 新增 {picResult.Added} 张，更新 {picResult.Updated} 张，跳过 {picResult.Skipped} 个文件夹";

                StatusMessage = $"{mediaMsg}；{pictureMsg}";
            }
            catch (Exception ex)
            {
                CoreLogger.Error("保存配置并扫描媒体库失败。", ex);
                StatusMessage = $"保存失败：{ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }
    }
}
