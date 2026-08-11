using FamilyTheater.Core.Data;
using FamilyTheater.Core.Services;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Input;
using LoginWindow.Models;
using ReactiveUI;

namespace LoginWindow.Views
{
    public partial class HomeWindow : Window, IViewFor<HomeWindowModel>
    {
        private readonly HomeWindowModel _viewModel;

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
        object? IViewFor.ViewModel { get => ViewModel; set => throw new NotImplementedException(); }


        public HomeWindow(HomeWindowModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = viewModel;
            ViewModel = viewModel;
            this.InputBindings.Add(new KeyBinding(viewModel.PrevPageCmd, Key.Left, ModifierKeys.None));
            this.InputBindings.Add(new KeyBinding(viewModel.NextPageCmd, Key.Right, ModifierKeys.None));
            this.Loaded += HomeWindow_Loaded;
        }

        private async void HomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadMoviesAsync();
        }

        /// <summary>
        /// 电影卡片左键点击 → 打开播放弹窗。
        /// </summary>
        private async void MovieCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Movie movie)
            {
                if (string.IsNullOrEmpty(movie.VideoFilePath) || !System.IO.File.Exists(movie.VideoFilePath))
                {
                    if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{movie.VideoFilePath}\n\n是否从数据库删除该记录？"))
                    {
                        await _viewModel.MovieService.DeleteMovieAsync(movie.Id);
                        await _viewModel.LoadMoviesAsync();
                    }
                    return;
                }
                var player = new PlayerWindow(movie.VideoFilePath);
                player.Owner = this;
                player.Show();
            }
        }

        /// <summary>
        /// 电影卡片右键点击 → 打开详情弹窗（管理标签）。
        /// </summary>
        private async void MovieCard_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Movie movie)
            {
                if (string.IsNullOrEmpty(movie.FolderPath) || !System.IO.Directory.Exists(movie.FolderPath))
                {
                    if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{movie.FolderPath}\n\n是否从数据库删除该记录？"))
                    {
                        await _viewModel.MovieService.DeleteMovieAsync(movie.Id);
                        await _viewModel.LoadMoviesAsync();
                    }
                    return;
                }
                var detail = new MovieDetailWindow(movie, _viewModel.MovieService);
                detail.Owner = this;
                detail.ShowDialog();
                // 关闭后刷新海报列表（标签可能在详情里改过）
                _ = _viewModel.LoadMoviesAsync();
            }
        }

        /// <summary>
        /// 图片卡片左键点击 → 打开大图预览弹窗。
        /// </summary>
        private async void PictureCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Picture picture)
            {
                if (string.IsNullOrEmpty(picture.FilePath) || !System.IO.File.Exists(picture.FilePath))
                {
                    if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{picture.FilePath}\n\n是否从数据库删除该记录？"))
                    {
                        await _viewModel.PictureService.DeletePictureAsync(picture.Id);
                        await _viewModel.LoadPicturesAsync();
                    }
                    return;
                }
                var viewer = new PictureViewerWindow(picture.FilePath);
                viewer.Owner = this;
                viewer.Show();
            }
        }

        /// <summary>
        /// 图片卡片右键点击 → 打开详情弹窗（管理标签）。
        /// </summary>
        private async void PictureCard_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Picture picture)
            {
                if (string.IsNullOrEmpty(picture.FilePath) || !System.IO.File.Exists(picture.FilePath))
                {
                    if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{picture.FilePath}\n\n是否从数据库删除该记录？"))
                    {
                        await _viewModel.PictureService.DeletePictureAsync(picture.Id);
                        await _viewModel.LoadPicturesAsync();
                    }
                    return;
                }
                var detail = new PictureDetailWindow(picture, _viewModel.PictureService);
                detail.Owner = this;
                detail.ShowDialog();
                // 关闭后刷新图片列表（标签可能在详情里改过）
                _ = _viewModel.LoadPicturesAsync();
            }
        }
    }
}
