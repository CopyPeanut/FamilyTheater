using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LoginWindow.Views
{
    public partial class PictureDetailWindow : Window
    {
        private readonly Picture _picture;
        private readonly IPictureService _pictureService;
        private readonly IAppLogger _logger;
        private readonly ObservableCollection<DetailTagViewModel> _allTags = new();
        private readonly List<DetailTagViewModel> _allAvailableTags = new();
        private readonly ObservableCollection<string> _pictureTags = new();

        public PictureDetailWindow(Picture picture, IPictureService pictureService, IAppLogger logger)
        {
            InitializeComponent();
            _picture = picture;
            _pictureService = pictureService;
            _logger = logger;

            TitleInput.Text = picture.FileName;
            FolderPathText.Text = picture.FilePath;

            if (!string.IsNullOrEmpty(picture.FilePath))
            {
                try
                {
                    PosterImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri(picture.FilePath, UriKind.Absolute));
                    PosterPlaceholder.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    _logger.Warn($"加载图片详情预览失败：PictureId={picture.Id}，FilePath={picture.FilePath}");
                    PosterImage.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                PosterImage.Visibility = Visibility.Collapsed;
            }

            _pictureTags.Clear();
            if (picture.PictureTags != null)
            {
                foreach (var tag in picture.PictureTags)
                {
                    _pictureTags.Add(tag.TagName);
                }
            }

            PictureTagsList.ItemsSource = _pictureTags;
            AllTagsList.ItemsSource = _allTags;
            _ = LoadAllTagsAsync();
        }

        private async Task LoadAllTagsAsync()
        {
            var tags = await _pictureService.GetAllTagsAsync();
            var freshPicture = await _pictureService.GetPictureByIdAsync(_picture.Id);

            _pictureTags.Clear();
            if (freshPicture?.PictureTags != null)
            {
                foreach (var tag in freshPicture.PictureTags)
                {
                    _pictureTags.Add(tag.TagName);
                }
            }

            _allAvailableTags.Clear();
            foreach (var name in tags)
            {
                var viewModel = new DetailTagViewModel(name)
                {
                    IsSelected = _pictureTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase))
                };
                _allAvailableTags.Add(viewModel);
            }

            ApplyTagFilter();
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
                await _pictureService.AddTagToPictureAsync(_picture.Id, tagViewModel.Name);
                if (!_pictureTags.Any(t => t.Equals(tagViewModel.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _pictureTags.Add(tagViewModel.Name);
                }
            }
            else
            {
                await _pictureService.RemoveTagFromPictureAsync(_picture.Id, tagViewModel.Name);
                var tagToRemove = _pictureTags.FirstOrDefault(t =>
                    t.Equals(tagViewModel.Name, StringComparison.OrdinalIgnoreCase));
                if (tagToRemove != null)
                {
                    _pictureTags.Remove(tagToRemove);
                }
            }
        }

        private async void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { DataContext: string tagName })
            {
                return;
            }

            await _pictureService.RemoveTagFromPictureAsync(_picture.Id, tagName);
            _pictureTags.Remove(tagName);

            var tagViewModel = _allAvailableTags.FirstOrDefault(t =>
                t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (tagViewModel != null)
            {
                tagViewModel.IsSelected = false;
            }
        }

        private void NewTagInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyTagFilter();
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

            await _pictureService.AddTagToPictureAsync(_picture.Id, name);

            if (!_pictureTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _pictureTags.Add(name);
            }

            var existing = _allAvailableTags.FirstOrDefault(t =>
                t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                _allAvailableTags.Add(new DetailTagViewModel(name) { IsSelected = true });
            }
            else
            {
                existing.IsSelected = true;
            }

            NewTagInput.Text = string.Empty;
            ApplyTagFilter();
        }

        private void ApplyTagFilter()
        {
            var keyword = NewTagInput.Text.Trim();
            var filteredTags = string.IsNullOrEmpty(keyword)
                ? _allAvailableTags
                : _allAvailableTags
                    .Where(tag => tag.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            _allTags.Clear();
            foreach (var tag in filteredTags)
            {
                _allTags.Add(tag);
            }
        }

        private async void DeleteLocalFile_Click(object sender, RoutedEventArgs e)
        {
            var message = File.Exists(_picture.FilePath)
                ? $"将删除以下图片本地文件，并从列表中移除该图片。是否继续？\n\n{_picture.FileName}\n{_picture.FilePath}"
                : $"当前图片本地文件不存在，将只从列表中移除该图片记录。是否继续？\n\n{_picture.FileName}\n{_picture.FilePath}";

            if (!CustomMessageBox.ShowDialog(message))
            {
                return;
            }

            var deleteButton = sender as System.Windows.Controls.Button;
            if (deleteButton != null)
            {
                deleteButton.IsEnabled = false;
            }

            try
            {
                await _pictureService.DeletePictureAsync(_picture.Id, deleteLocalFile: true);
                Close();
            }
            catch (Exception ex)
            {
                _logger.Error($"Delete picture from detail failed. PictureId={_picture.Id}, FilePath={_picture.FilePath}", ex);
                CustomMessageBox.Show($"删除失败：\n{ex.Message}", FamilyTheater.Core.Enum.LogLevel.ERROR);

                if (deleteButton != null)
                {
                    deleteButton.IsEnabled = true;
                }
            }
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
