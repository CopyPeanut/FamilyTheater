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

        [Reactive] public string MediaRootPath { get; set; }
        [Reactive] public string StatusMessage { get; set; }

        public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public ConfigWindowModel(ISettingService settingService)
        {
            _settingService = settingService;

            SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
            BrowseCommand = ReactiveCommand.CreateFromTask(BrowseAsync);

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
            try
            {
                await _settingService.SetMediaRootPathAsync(MediaRootPath ?? string.Empty);
                StatusMessage = "保存成功";
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存失败：{ex.Message}";
            }
        }
    }
}