using FamilyTheater.Core.Data;
using FamilyTheater.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginWindow.Views
{
    public partial class PictureDetailWindow : Window
    {
        private readonly Picture _picture;
        private readonly IPictureService _pictureService;
        private readonly ObservableCollection<DetailTagViewModel> _allTags = new();
        private readonly ObservableCollection<string> _pictureTags = new();

        public PictureDetailWindow(Picture picture, IPictureService pictureService)
        {
            InitializeComponent();
            _picture = picture;
            _pictureService = pictureService;

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
                foreach (var pt in picture.PictureTags)
                    _pictureTags.Add(pt.TagName);
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
                foreach (var pt in freshPicture.PictureTags)
                    _pictureTags.Add(pt.TagName);
            }

            _allTags.Clear();
            foreach (var name in tags)
            {
                var vm = new DetailTagViewModel(name);
                vm.IsSelected = _pictureTags.Any(t =>
                    t.Equals(name, StringComparison.OrdinalIgnoreCase));
                _allTags.Add(vm);
            }
        }

        /// <summary>
        /// 点击已有标签：切换选中状态 → 立即写库 → 刷新 UI。
        /// </summary>
        private async void ToggleTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is DetailTagViewModel tagVm)
            {
                tagVm.IsSelected = !tagVm.IsSelected;

                if (tagVm.IsSelected)
                {
                    await _pictureService.AddTagToPictureAsync(_picture.Id, tagVm.Name);
                    if (!_pictureTags.Any(t => t.Equals(tagVm.Name, StringComparison.OrdinalIgnoreCase)))
                        _pictureTags.Add(tagVm.Name);
                }
                else
                {
                    await _pictureService.RemoveTagFromPictureAsync(_picture.Id, tagVm.Name);
                    var toRemove = _pictureTags.FirstOrDefault(t =>
                        t.Equals(tagVm.Name, StringComparison.OrdinalIgnoreCase));
                    if (toRemove != null)
                        _pictureTags.Remove(toRemove);
                }
            }
        }

        /// <summary>
        /// 点击所属标签：从图片移除 → 同步标签选择区。
        /// </summary>
        private async void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is string tagName)
            {
                await _pictureService.RemoveTagFromPictureAsync(_picture.Id, tagName);

                _pictureTags.Remove(tagName);

                var vm = _allTags.FirstOrDefault(t =>
                    t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                if (vm != null)
                    vm.IsSelected = false;
            }
        }

        /// <summary>
        /// 新建标签输入栏回车 → 添加。
        /// </summary>
        private void NewTagInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddNewTag_Click(sender, e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 点击添加按钮 → 创建新标签并添加到图片。
        /// </summary>
        private async void AddNewTag_Click(object sender, RoutedEventArgs e)
        {
            var name = NewTagInput.Text.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            await _pictureService.AddTagToPictureAsync(_picture.Id, name);

            if (!_pictureTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase)))
                _pictureTags.Add(name);

            var vm = _allTags.FirstOrDefault(t =>
                t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (vm == null)
            {
                vm = new DetailTagViewModel(name) { IsSelected = true };
                _allTags.Add(vm);
            }
            else
            {
                vm.IsSelected = true;
            }

            NewTagInput.Text = string.Empty;
        }
    }
}
