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

        public GameDetailWindow(Game game, IGameService gameService, IAppLogger logger)
        {
            InitializeComponent();
            _game = game;
            _gameService = gameService;
            _logger = logger;

            TitleInput.Text = game.Title;
            FolderPathText.Text = game.FolderPath;
            LaunchPathText.Text = game.LaunchPath ?? string.Empty;
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
            _ = LoadAllTagsAsync();
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
                PosterImage.Source = new BitmapImage(new Uri(posterPath, UriKind.Absolute));
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
