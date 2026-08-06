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

        [Reactive] public string MediaRootPath { get; set; } = string.Empty;
        [Reactive] public string StatusMessage { get; set; } = string.Empty;
        [Reactive] public bool IsScanning { get; set; }

        public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public ConfigWindowModel(ISettingService settingService, IMovieService movieService)
        {
            _settingService = settingService;
            _movieService = movieService;

            // 保存命令在扫描期间禁用
            var canSave = this.WhenAnyValue(x => x.IsScanning, scanning => !scanning);
            SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
            BrowseCommand = ReactiveCommand.CreateFromTask(BrowseAsync, canSave);

            // 加载已有配置
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var path = await _settingService.GetMediaRootPathAsync();
            MediaRootPath = path ?? string.Empty;
        }

        private async Task BrowseAsync()
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "选择媒体根目录",
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                MediaRootPath = dialog.SelectedPath;
            }
        }

        private async Task SaveAsync()
        {
            IsScanning = true;
            StatusMessage = "正在扫描入库...";
            try
            {
                // 1. 保存媒体根目录到数据库
                await _settingService.SetMediaRootPathAsync(MediaRootPath ?? string.Empty);

                // 2. 触发扫描入库
                var result = await _movieService.ScanLibraryAsync();

                // 3. 显示扫描结果
                StatusMessage = result.Added == 0 && result.Updated == 0 && result.Skipped == 0
                    ? "保存成功，未发现可入库的视频文件"
                    : $"保存成功 — 新增 {result.Added} 部，更新 {result.Updated} 部，跳过 {result.Skipped} 个文件夹";
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存失败：{ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }
    }
}
