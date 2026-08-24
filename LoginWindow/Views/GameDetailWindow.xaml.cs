using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LoginWindow.Views
{
    public partial class GameDetailWindow : Window
    {
        private readonly Game _game;
        private readonly IGameService _gameService;
        private readonly IAppLogger _logger;
        private readonly ObservableCollection<DetailTagViewModel> _allTags = new();
        private readonly ObservableCollection<string> _gameTags = new();
        private readonly ObservableCollection<string> _screenshots = new();

        public GameDetailWindow(Game game, IGameService gameService, IAppLogger logger)
        {
            InitializeComponent();
            _game = game;
            _gameService = gameService;
            _logger = logger;

            TitleInput.Text = game.Title;
            FolderPathText.Text = game.FolderPath;
            LaunchPathText.Text = game.LaunchPath ?? string.Empty;
            ScreenshotRootPathText.Text = game.ScreenshotRootPath ?? string.Empty;
            LoadPoster(game.PosterPath);

            if (game.GameTags != null)
            {
                foreach (var tag in game.GameTags)
                {
                    _gameTags.Add(tag.TagName);
                }
            }

            GameTagsList.ItemsSource = _gameTags;
            AllTagsList.ItemsSource = _allTags;
            ScreenshotList.ItemsSource = _screenshots;
            _ = LoadAllTagsAsync();
            _ = LoadScreenshotsAsync();
        }

        private async Task LoadAllTagsAsync()
        {
            var tags = await _gameService.GetAllTagsAsync();
            var freshGame = await _gameService.GetGameByIdAsync(_game.Id);

            _gameTags.Clear();
            if (freshGame?.GameTags != null)
            {
                foreach (var tag in freshGame.GameTags)
                {
                    _gameTags.Add(tag.TagName);
                }
            }

            _allTags.Clear();
            foreach (var name in tags)
            {
                _allTags.Add(new DetailTagViewModel(name)
                {
                    IsSelected = _gameTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase))
                });
            }
        }

        private void LoadPoster(string? posterPath)
        {
            if (string.IsNullOrWhiteSpace(posterPath) || !File.Exists(posterPath))
            {
                PosterImage.Visibility = Visibility.Collapsed;
                PosterPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(posterPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                PosterImage.Source = image;
                PosterImage.Visibility = Visibility.Visible;
                PosterPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _logger.Warn($"加载游戏海报失败：GameId={_game.Id}, PosterPath={posterPath}", ex);
                PosterImage.Visibility = Visibility.Collapsed;
                PosterPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private async void SelectPoster_Click(object sender, RoutedEventArgs e)
        {
            var selectedPath = ChoosePosterImage(_game.PosterPath);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            var updated = await _gameService.SetPosterPathAsync(_game.Id, selectedPath);
            if (updated == null)
            {
                CustomMessageBox.Show("海报保存失败，请确认选择的是有效图片文件。");
                return;
            }

            _game.PosterPath = updated.PosterPath;
            LoadPoster(updated.PosterPath);
        }

        private string? ChoosePosterImage(string? currentPosterPath)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择游戏海报",
                Filter = "图片文件 (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
                CheckFileExists = true,
                Multiselect = false
            };

            var initialDirectory = GetInitialPosterDirectory(currentPosterPath);
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        private string? GetInitialPosterDirectory(string? currentPosterPath)
        {
            if (!string.IsNullOrWhiteSpace(currentPosterPath))
            {
                var currentFolder = Path.GetDirectoryName(currentPosterPath);
                if (!string.IsNullOrWhiteSpace(currentFolder) && Directory.Exists(currentFolder))
                {
                    return currentFolder;
                }
            }

            return !string.IsNullOrWhiteSpace(_game.FolderPath) && Directory.Exists(_game.FolderPath)
                ? _game.FolderPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

        private async void TitleInput_LostFocus(object sender, RoutedEventArgs e)
        {
            await DoRenameAsync();
        }

        private void TitleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _ = DoRenameAsync();
                e.Handled = true;
            }
        }

        private async Task DoRenameAsync()
        {
            var newTitle = TitleInput.Text.Trim();
            if (string.IsNullOrEmpty(newTitle))
            {
                CustomMessageBox.Show("标题不能为空");
                return;
            }

            if (newTitle.Equals(_game.Title, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var updated = await _gameService.RenameGameAsync(_game.Id, newTitle);
                if (updated == null)
                {
                    CustomMessageBox.Show("改名失败，请稍后再试。");
                    return;
                }

                _game.Title = updated.Title;
                TitleInput.Text = updated.Title;
            }
            catch (Exception ex)
            {
                _logger.Error($"游戏改名异常：GameId={_game.Id}, Title={newTitle}", ex);
                CustomMessageBox.Show($"改名失败：{ex.Message}");
            }
        }

        private async void SelectLaunch_Click(object sender, RoutedEventArgs e)
        {
            var selectedPath = ChooseExecutable();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            var updated = await _gameService.SetLaunchPathAsync(_game.Id, selectedPath);
            if (updated?.LaunchPath == null)
            {
                CustomMessageBox.Show("启动项保存失败，请确认选择的是游戏目录内的 exe。");
                return;
            }

            _game.LaunchPath = updated.LaunchPath;
            LaunchPathText.Text = updated.LaunchPath;
        }

        private async void SelectScreenshotRoot_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = "选择游戏截图目录",
                UseDescriptionForTitle = true,
                SelectedPath = GetInitialScreenshotDirectory()
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
            {
                return;
            }

            var updated = await _gameService.SetScreenshotRootPathAsync(_game.Id, dialog.SelectedPath);
            if (updated == null)
            {
                CustomMessageBox.Show("截图目录保存失败，请确认目录存在。");
                return;
            }

            _game.ScreenshotRootPath = updated.ScreenshotRootPath;
            ScreenshotRootPathText.Text = updated.ScreenshotRootPath ?? string.Empty;
            await LoadScreenshotsAsync();
        }

        private string GetInitialScreenshotDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_game.ScreenshotRootPath) &&
                Directory.Exists(_game.ScreenshotRootPath))
            {
                return _game.ScreenshotRootPath;
            }

            return !string.IsNullOrWhiteSpace(_game.FolderPath) && Directory.Exists(_game.FolderPath)
                ? _game.FolderPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

        private async Task LoadScreenshotsAsync()
        {
            var images = await _gameService.GetScreenshotImagesAsync(_game.Id);

            _screenshots.Clear();
            foreach (var image in images)
            {
                _screenshots.Add(image);
            }

            NoScreenshotsText.Visibility = _screenshots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ScreenshotScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var direction = e.Delta > 0 ? -1 : 1;
            ScreenshotScrollViewer.ScrollToHorizontalOffset(ScreenshotScrollViewer.HorizontalOffset + direction * 120);
            e.Handled = true;
        }

        private void Screenshot_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: string imagePath })
            {
                return;
            }

            if (!File.Exists(imagePath))
            {
                CustomMessageBox.Show($"图片不存在：\n{imagePath}");
                _ = LoadScreenshotsAsync();
                return;
            }

            var viewer = new PictureViewerWindow(imagePath, _logger);
            viewer.Show();
        }

        private string? ChooseExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择游戏启动程序",
                Filter = "Executable (*.exe)|*.exe",
                CheckFileExists = true,
                Multiselect = false
            };

            if (!string.IsNullOrWhiteSpace(_game.FolderPath) && Directory.Exists(_game.FolderPath))
            {
                dialog.InitialDirectory = _game.FolderPath;
            }

            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        private async void ToggleTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { DataContext: DetailTagViewModel tagViewModel })
            {
                return;
            }

            tagViewModel.IsSelected = !tagViewModel.IsSelected;

            if (tagViewModel.IsSelected)
            {
                await _gameService.AddTagToGameAsync(_game.Id, tagViewModel.Name);
                if (!_gameTags.Any(t => t.Equals(tagViewModel.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _gameTags.Add(tagViewModel.Name);
                }
            }
            else
            {
                await _gameService.RemoveTagFromGameAsync(_game.Id, tagViewModel.Name);
                var tagToRemove = _gameTags.FirstOrDefault(t =>
                    t.Equals(tagViewModel.Name, StringComparison.OrdinalIgnoreCase));
                if (tagToRemove != null)
                {
                    _gameTags.Remove(tagToRemove);
                }
            }
        }

        private async void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { DataContext: string tagName })
            {
                return;
            }

            await _gameService.RemoveTagFromGameAsync(_game.Id, tagName);
            _gameTags.Remove(tagName);

            var tagViewModel = _allTags.FirstOrDefault(t =>
                t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (tagViewModel != null)
            {
                tagViewModel.IsSelected = false;
            }
        }

        private void NewTagInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddNewTag_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void AddNewTag_Click(object sender, RoutedEventArgs e)
        {
            var name = NewTagInput.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            await _gameService.AddTagToGameAsync(_game.Id, name);

            if (!_gameTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _gameTags.Add(name);
            }

            var existing = _allTags.FirstOrDefault(t =>
                t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                _allTags.Add(new DetailTagViewModel(name) { IsSelected = true });
            }
            else
            {
                existing.IsSelected = true;
            }

            NewTagInput.Text = string.Empty;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
    }
}
