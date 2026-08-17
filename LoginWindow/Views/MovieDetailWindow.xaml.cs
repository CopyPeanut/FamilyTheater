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
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LoginWindow.Views
{
    public class DetailTagViewModel : ReactiveUI.ReactiveObject
    {
        public string Name { get; }

        [ReactiveUI.Fody.Helpers.Reactive]
        public bool IsSelected { get; set; }

        public DetailTagViewModel(string name)
        {
            Name = name;
        }
    }

    public partial class MovieDetailWindow : Window
    {
        private readonly Movie _movie;
        private readonly IMovieService _movieService;
        private readonly IAppLogger _logger;
        private readonly ObservableCollection<DetailTagViewModel> _allTags = new();
        private readonly ObservableCollection<string> _movieTags = new();

        public MovieDetailWindow(Movie movie, IMovieService movieService, IAppLogger logger)
        {
            InitializeComponent();
            _movie = movie;
            _movieService = movieService;
            _logger = logger;

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
                    _logger.Warn($"加载电影封面失败：MovieId={movie.Id}，PosterPath={movie.PosterPath}");
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
                foreach (var tag in movie.MovieTags)
                {
                    _movieTags.Add(tag.TagName);
                }
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
                foreach (var tag in freshMovie.MovieTags)
                {
                    _movieTags.Add(tag.TagName);
                }
            }

            _allTags.Clear();
            foreach (var name in tags)
            {
                var viewModel = new DetailTagViewModel(name)
                {
                    IsSelected = _movieTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase))
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
                _logger.Warn($"电影重命名校验失败：标题为空。MovieId={_movie.Id}");
                CustomMessageBox.Show("标题不能为空");
                return;
            }

            if (newTitle.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _logger.Warn($"电影重命名校验失败：标题包含非法字符。MovieId={_movie.Id}，Title={newTitle}");
                CustomMessageBox.Show("标题包含非法字符，不能用作文件夹名");
                return;
            }

            if (newTitle.Equals(_movie.Title, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var folder = Path.GetDirectoryName(_movie.VideoFilePath);
            var newVideoPath = Path.Combine(folder ?? string.Empty, newTitle + Path.GetExtension(_movie.VideoFilePath));
            if (File.Exists(newVideoPath) &&
                !string.Equals(newVideoPath, _movie.VideoFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warn($"电影重命名校验失败：目标视频文件已存在。MovieId={_movie.Id}，TargetPath={newVideoPath}");
                CustomMessageBox.Show($"目标视频文件已存在：{newVideoPath}");
                return;
            }

            try
            {
                var updated = await _movieService.RenameMovieAsync(_movie.Id, newTitle);
                if (updated == null)
                {
                    _logger.Warn($"电影重命名失败：服务返回空。MovieId={_movie.Id}，Title={newTitle}");
                    CustomMessageBox.Show("改名失败，请检查文件夹是否被占用或权限不足");
                    return;
                }

                _movie.Title = updated.Title;
                _movie.FolderPath = updated.FolderPath;
                _movie.VideoFilePath = updated.VideoFilePath;
                _movie.PosterPath = updated.PosterPath;

                TitleInput.Text = updated.Title;
                FolderPathText.Text = updated.FolderPath;

                if (!string.IsNullOrEmpty(updated.PosterPath))
                {
                    try
                    {
                        PosterImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                            new Uri(updated.PosterPath, UriKind.Absolute));
                        PosterImage.Visibility = Visibility.Visible;
                        PosterPlaceholder.Visibility = Visibility.Collapsed;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"刷新电影封面失败：MovieId={_movie.Id}，PosterPath={updated.PosterPath}", ex);
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.Error($"电影重命名异常：MovieId={_movie.Id}，Title={newTitle}", ex);
                CustomMessageBox.Show($"改名失败：{ex.Message}");
            }
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
                await _movieService.AddTagToMovieAsync(_movie.Id, tagViewModel.Name);
                if (!_movieTags.Any(t => t.Equals(tagViewModel.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _movieTags.Add(tagViewModel.Name);
                }
            }
            else
            {
                await _movieService.RemoveTagFromMovieAsync(_movie.Id, tagViewModel.Name);
                var tagToRemove = _movieTags.FirstOrDefault(t =>
                    t.Equals(tagViewModel.Name, StringComparison.OrdinalIgnoreCase));
                if (tagToRemove != null)
                {
                    _movieTags.Remove(tagToRemove);
                }
            }
        }

        private async void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { DataContext: string tagName })
            {
                return;
            }

            await _movieService.RemoveTagFromMovieAsync(_movie.Id, tagName);
            _movieTags.Remove(tagName);

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

            await _movieService.AddTagToMovieAsync(_movie.Id, name);

            if (!_movieTags.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _movieTags.Add(name);
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
