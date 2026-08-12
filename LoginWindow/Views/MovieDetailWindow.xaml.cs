using FamilyTheater.Core.Data;
using FamilyTheater.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginWindow.Views
{
    /// <summary>
    /// 标签 UI 包装：Name 显示名称，IsSelected 选中状态。
    /// </summary>
    public class DetailTagViewModel : ReactiveUI.ReactiveObject
    {
        public string Name { get; }
        [ReactiveUI.Fody.Helpers.Reactive] public bool IsSelected { get; set; }

        public DetailTagViewModel(string name)
        {
            Name = name;
        }
    }

    public partial class MovieDetailWindow : Window
    {
        private readonly Movie _movie;
        private readonly IMovieService _movieService;
        private readonly ObservableCollection<DetailTagViewModel> _allTags = new();
        private readonly ObservableCollection<string> _movieTags = new();

        public MovieDetailWindow(Movie movie, IMovieService movieService)
        {
            InitializeComponent();
            _movie = movie;
            _movieService = movieService;

            TitleInput.Text = movie.Title;
            FolderPathText.Text = movie.FolderPath;

            if (!string.IsNullOrEmpty(movie.PosterPath))
            {
                try
                {
                    PosterImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri(movie.PosterPath, UriKind.Absolute));
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

            _movieTags.Clear();
            if (movie.MovieTags != null)
            {
                foreach (var mt in movie.MovieTags)
                    _movieTags.Add(mt.TagName);
            }
            MovieTagsList.ItemsSource = _movieTags;

            AllTagsList.ItemsSource = _allTags;
            _ = LoadAllTagsAsync();
        }

        private async Task LoadAllTagsAsync()
        {
            var tags = await _movieService.GetAllTagsAsync();

            var freshMovie = await _movieService.GetMovieByIdAsync(_movie.Id);
            _movieTags.Clear();
            if (freshMovie?.MovieTags != null)
            {
                foreach (var mt in freshMovie.MovieTags)
                    _movieTags.Add(mt.TagName);
            }

            _allTags.Clear();
            foreach (var name in tags)
            {
                var vm = new DetailTagViewModel(name);
                vm.IsSelected = _movieTags.Any(t =>
                    t.Equals(name, StringComparison.OrdinalIgnoreCase));
                _allTags.Add(vm);
            }
        }

        /// <summary>
        /// 改名按钮 → 重命名电影（改标题+改文件夹名+更新路径+写库）。
        /// </summary>
        /// <summary>
        /// 标题输入框失焦 → 自动改名。
        /// </summary>
        private async void TitleInput_LostFocus(object sender, RoutedEventArgs e)
        {
            await DoRenameAsync();
        }

        /// <summary>
        /// 标题输入框回车 → 改名。
        /// </summary>
        private void TitleInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

            if (newTitle.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                CustomMessageBox.Show("标题包含非法字符，不能用作文件夹名");
                return;
            }

            if (newTitle.Equals(_movie.Title, StringComparison.OrdinalIgnoreCase))
                return; // 没变

            // 检查目标文件夹是否已存在
            var parentDir = Path.GetDirectoryName(_movie.FolderPath);
            var newFolderPath = Path.Combine(parentDir ?? "", newTitle);
            if (Directory.Exists(newFolderPath) &&
                !string.Equals(newFolderPath, _movie.FolderPath, StringComparison.OrdinalIgnoreCase))
            {
                CustomMessageBox.Show($"目标文件夹已存在：{newFolderPath}");
                return;
            }

            try
            {
                var updated = await _movieService.RenameMovieAsync(_movie.Id, newTitle);
                if (updated != null)
                {
                    // 更新本地状态和 UI
                    _movie.Title = updated.Title;
                    _movie.FolderPath = updated.FolderPath;
                    _movie.VideoFilePath = updated.VideoFilePath;
                    _movie.PosterPath = updated.PosterPath;

                    TitleInput.Text = updated.Title;
                    FolderPathText.Text = updated.FolderPath;

                    // 更新封面（海报路径可能变了）
                    if (!string.IsNullOrEmpty(updated.PosterPath))
                    {
                        try
                        {
                            PosterImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                                new Uri(updated.PosterPath, UriKind.Absolute));
                            PosterImage.Visibility = Visibility.Visible;
                            PosterPlaceholder.Visibility = Visibility.Collapsed;
                        }
                        catch { }
                    }

                    // 标记弹窗结果，让父窗口知道需要刷新
                    DialogResult = true;
                }
                else
                {
                    CustomMessageBox.Show("改名失败，请检查文件夹是否被占用或权限不足");
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"改名失败：{ex.Message}");
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
                    await _movieService.AddTagToMovieAsync(_movie.Id, tagVm.Name);
                    if (!_movieTags.Any(t => t.Equals(tagVm.Name, StringComparison.OrdinalIgnoreCase)))
                        _movieTags.Add(tagVm.Name);
                }
                else
                {
                    await _movieService.RemoveTagFromMovieAsync(_movie.Id, tagVm.Name);
                    var toRemove = _movieTags.FirstOrDefault(t =>
                        t.Equals(tagVm.Name, StringComparison.OrdinalIgnoreCase));
                    if (toRemove != null)
                        _movieTags.Remove(toRemove);
                }
            }
        }

        /// <summary>
        /// 点击所属标签：从电影移除 → 同步标签选择区。
        /// </summary>
        private async void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is string tagName)
            {
                await _movieService.RemoveTagFromMovieAsync(_movie.Id, tagName);

                _movieTags.Remove(tagName);

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
        /// 点击添加按钮 → 创建新标签并添加到电影。
        /// </summary>
        private async void AddNewTag_Click(object sender, RoutedEventArgs e)
        {
            var name = NewTagInput.Text.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            await _movieService.AddTagToMovieAsync(_movie.Id, name);

            if (!_movieTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase)))
                _movieTags.Add(name);

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

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
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
