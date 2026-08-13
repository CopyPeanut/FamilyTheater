using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using LoginWindow.Models;
using ReactiveUI;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace LoginWindow.Views
{
    public partial class HomeWindow : Window, IViewFor<HomeWindowModel>
    {
        private readonly HomeWindowModel _viewModel;
        private readonly IAppLogger _logger;

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(HomeWindowModel),
                typeof(HomeWindow),
                new PropertyMetadata(null));

        HomeWindowModel IViewFor<HomeWindowModel>.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value;
        }

        public HomeWindowModel ViewModel
        {
            get => (HomeWindowModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => throw new NotImplementedException();
        }

        public HomeWindow(HomeWindowModel viewModel, IAppLogger logger)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _logger = logger;

            DataContext = viewModel;
            ViewModel = viewModel;
            InputBindings.Add(new KeyBinding(viewModel.PrevPageCmd, Key.Left, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(viewModel.NextPageCmd, Key.Right, ModifierKeys.None));
            Loaded += HomeWindow_Loaded;
        }

        private async void HomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadMoviesAsync();
        }

        private async void MovieCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: Movie movie })
            {
                return;
            }

            if (string.IsNullOrEmpty(movie.VideoFilePath) || !File.Exists(movie.VideoFilePath))
            {
                _logger.Warn($"打开电影失败：视频文件不存在。MovieId={movie.Id}，VideoFilePath={movie.VideoFilePath}");
                if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{movie.VideoFilePath}\n\n是否从数据库删除该记录？"))
                {
                    _logger.Info($"用户确认删除失效电影记录：MovieId={movie.Id}，VideoFilePath={movie.VideoFilePath}");
                    await _viewModel.MovieService.DeleteMovieAsync(movie.Id);
                    await _viewModel.LoadMoviesAsync();
                }
                return;
            }

            var player = new PlayerWindow(movie.VideoFilePath, _logger)
            {
                Owner = this
            };
            player.Show();
        }

        private async void MovieCard_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: Movie movie })
            {
                return;
            }

            if (string.IsNullOrEmpty(movie.FolderPath) || !Directory.Exists(movie.FolderPath))
            {
                _logger.Warn($"打开电影详情失败：电影文件夹不存在。MovieId={movie.Id}，FolderPath={movie.FolderPath}");
                if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{movie.FolderPath}\n\n是否从数据库删除该记录？"))
                {
                    _logger.Info($"用户确认删除失效电影记录：MovieId={movie.Id}，FolderPath={movie.FolderPath}");
                    await _viewModel.MovieService.DeleteMovieAsync(movie.Id);
                    await _viewModel.LoadMoviesAsync();
                }
                return;
            }

            var detail = new MovieDetailWindow(movie, _viewModel.MovieService, _logger)
            {
                Owner = this
            };
            detail.ShowDialog();
            _ = _viewModel.LoadMoviesAsync();
        }

        private async void PictureCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: Picture picture })
            {
                return;
            }

            if (string.IsNullOrEmpty(picture.FilePath) || !File.Exists(picture.FilePath))
            {
                _logger.Warn($"打开图片失败：图片文件不存在。PictureId={picture.Id}，FilePath={picture.FilePath}");
                if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{picture.FilePath}\n\n是否从数据库删除该记录？"))
                {
                    _logger.Info($"用户确认删除失效图片记录：PictureId={picture.Id}，FilePath={picture.FilePath}");
                    await _viewModel.PictureService.DeletePictureAsync(picture.Id);
                    await _viewModel.LoadPicturesAsync();
                }
                return;
            }

            var viewer = new PictureViewerWindow(picture.FilePath, _logger)
            {
                Owner = this
            };
            viewer.Show();
        }

        private async void PictureCard_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: Picture picture })
            {
                return;
            }

            if (string.IsNullOrEmpty(picture.FilePath) || !File.Exists(picture.FilePath))
            {
                _logger.Warn($"打开图片详情失败：图片文件不存在。PictureId={picture.Id}，FilePath={picture.FilePath}");
                if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{picture.FilePath}\n\n是否从数据库删除该记录？"))
                {
                    _logger.Info($"用户确认删除失效图片记录：PictureId={picture.Id}，FilePath={picture.FilePath}");
                    await _viewModel.PictureService.DeletePictureAsync(picture.Id);
                    await _viewModel.LoadPicturesAsync();
                }
                return;
            }

            var detail = new PictureDetailWindow(picture, _viewModel.PictureService, _logger)
            {
                Owner = this
            };
            detail.ShowDialog();
            _ = _viewModel.LoadPicturesAsync();
        }
    }
}
