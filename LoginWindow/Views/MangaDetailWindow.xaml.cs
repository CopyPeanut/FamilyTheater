using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
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
    public partial class MangaDetailWindow : Window
    {
        private readonly Manga _manga;
        private readonly IMangaService _mangaService;
        private readonly IAppLogger _logger;
        private readonly ObservableCollection<DetailTagViewModel> _allTags = new();
        private readonly ObservableCollection<string> _mangaTags = new();

        public MangaDetailWindow(Manga manga, IMangaService mangaService, IAppLogger logger)
        {
            InitializeComponent();
            _manga = manga;
            _mangaService = mangaService;
            _logger = logger;

            TitleInput.Text = manga.Title;
            FolderPathText.Text = manga.FilePath;
            LoadPoster(manga.PosterPath);

            _mangaTags.Clear();
            if (manga.MangaTags != null)
            {
                foreach (var tag in manga.MangaTags)
                {
                    _mangaTags.Add(tag.TagName);
                }
            }

            MangaTagsList.ItemsSource = _mangaTags;
            AllTagsList.ItemsSource = _allTags;
            _ = LoadAllTagsAsync();
        }

        private async Task LoadAllTagsAsync()
        {
            var tags = await _mangaService.GetAllTagsAsync();
            var freshManga = await _mangaService.GetMangaByIdAsync(_manga.Id);

            _mangaTags.Clear();
            if (freshManga?.MangaTags != null)
            {
                foreach (var tag in freshManga.MangaTags)
                {
                    _mangaTags.Add(tag.TagName);
                }
            }

            _allTags.Clear();
            foreach (var name in tags)
            {
                var viewModel = new DetailTagViewModel(name)
                {
                    IsSelected = _mangaTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase))
                };
                _allTags.Add(viewModel);
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
                _logger.Warn($"Manga rename validation failed: empty title. MangaId={_manga.Id}");
                CustomMessageBox.Show("标题不能为空");
                return;
            }

            if (newTitle.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _logger.Warn($"Manga rename validation failed: invalid title. MangaId={_manga.Id}, Title={newTitle}");
                CustomMessageBox.Show("标题包含非法字符，不能用作文件名");
                return;
            }

            if (newTitle.Equals(_manga.Title, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var folder = Path.GetDirectoryName(_manga.FilePath);
            var newFilePath = Path.Combine(folder ?? string.Empty, newTitle + Path.GetExtension(_manga.FilePath));
            if (File.Exists(newFilePath) &&
                !string.Equals(newFilePath, _manga.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warn($"Manga rename validation failed: target file exists. MangaId={_manga.Id}, TargetPath={newFilePath}");
                CustomMessageBox.Show($"目标文件已存在：{newFilePath}");
                return;
            }

            try
            {
                var updated = await _mangaService.RenameMangaAsync(_manga.Id, newTitle);
                if (updated == null)
                {
                    _logger.Warn($"Manga rename failed: service returned null. MangaId={_manga.Id}, Title={newTitle}");
                    CustomMessageBox.Show("改名失败，请检查文件是否被占用或权限不足");
                    return;
                }

                _manga.Title = updated.Title;
                _manga.FilePath = updated.FilePath;
                _manga.FolderPath = updated.FolderPath;
                _manga.PosterPath = updated.PosterPath;

                TitleInput.Text = updated.Title;
                FolderPathText.Text = updated.FilePath;
                LoadPoster(updated.PosterPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"Manga rename exception. MangaId={_manga.Id}, Title={newTitle}", ex);
                CustomMessageBox.Show($"改名失败：{ex.Message}");
            }
        }

        private void LoadPoster(string? posterPath)
        {
            if (string.IsNullOrWhiteSpace(posterPath) || !File.Exists(posterPath))
            {
                PosterImage.Source = null;
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
                _logger.Warn($"Load manga poster failed. MangaId={_manga.Id}, PosterPath={posterPath}", ex);
                PosterImage.Source = null;
                PosterImage.Visibility = Visibility.Collapsed;
                PosterPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private async void SelectPoster_Click(object sender, RoutedEventArgs e)
        {
            var selectedPath = ChoosePosterImage(_manga.PosterPath);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            var updated = await _mangaService.SetPosterPathAsync(_manga.Id, selectedPath);
            if (updated == null)
            {
                CustomMessageBox.Show("封面保存失败，请确认选择的是有效图片文件。");
                return;
            }

            _manga.PosterPath = updated.PosterPath;
            LoadPoster(updated.PosterPath);
        }

        private string? ChoosePosterImage(string? currentPosterPath)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择漫画封面",
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

            var mangaFolder = Path.GetDirectoryName(_manga.FilePath);
            return !string.IsNullOrWhiteSpace(mangaFolder) && Directory.Exists(mangaFolder)
                ? mangaFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
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
                await _mangaService.AddTagToMangaAsync(_manga.Id, tagViewModel.Name);
                if (!_mangaTags.Any(t => t.Equals(tagViewModel.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _mangaTags.Add(tagViewModel.Name);
                }
            }
            else
            {
                await _mangaService.RemoveTagFromMangaAsync(_manga.Id, tagViewModel.Name);
                var tagToRemove = _mangaTags.FirstOrDefault(t =>
                    t.Equals(tagViewModel.Name, StringComparison.OrdinalIgnoreCase));
                if (tagToRemove != null)
                {
                    _mangaTags.Remove(tagToRemove);
                }
            }
        }

        private async void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { DataContext: string tagName })
            {
                return;
            }

            await _mangaService.RemoveTagFromMangaAsync(_manga.Id, tagName);
            _mangaTags.Remove(tagName);

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

            await _mangaService.AddTagToMangaAsync(_manga.Id, name);

            if (!_mangaTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _mangaTags.Add(name);
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
