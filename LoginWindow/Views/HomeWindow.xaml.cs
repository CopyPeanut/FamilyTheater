using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using LoginWindow.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace LoginWindow.Views
{
    public partial class HomeWindow : Window, IViewFor<HomeWindowModel>
    {
        private readonly HomeWindowModel _viewModel;
        private readonly IAppLogger _logger;
        private readonly Func<Login> _loginWindowFactory;

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

        public HomeWindow(HomeWindowModel viewModel, IAppLogger logger, Func<Login> loginWindowFactory)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _logger = logger;
            _loginWindowFactory = loginWindowFactory;

            DataContext = viewModel;
            ViewModel = viewModel;
            InputBindings.Add(new KeyBinding(viewModel.PrevPageCmd, Key.Left, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(viewModel.NextPageCmd, Key.Right, ModifierKeys.None));
            viewModel.LogoutRequested += OnLogoutRequested;
            Loaded += HomeWindow_Loaded;
        }

        private async void HomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadMoviesAsync();
        }

        private void OnLogoutRequested()
        {
            var loginWindow = _loginWindowFactory();
            loginWindow.Show();
            Close();
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

            if (string.IsNullOrEmpty(movie.VideoFilePath) || !File.Exists(movie.VideoFilePath))
            {
                _logger.Warn($"打开电影详情失败：电影文件不存在。MovieId={movie.Id}，VideoFilePath={movie.VideoFilePath}");
                if (CustomMessageBox.ShowDialog($"当前文件不存在：\n{movie.VideoFilePath}\n\n是否从数据库删除该记录？"))
                {
                    _logger.Info($"用户确认删除失效电影记录：MovieId={movie.Id}，VideoFilePath={movie.VideoFilePath}");
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

        private async void GameCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: Game game })
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(game.LaunchPath) && File.Exists(game.LaunchPath))
            {
                LaunchGame(game.LaunchPath);
                return;
            }

            if (string.IsNullOrWhiteSpace(game.FolderPath) || !Directory.Exists(game.FolderPath))
            {
                _logger.Warn($"打开游戏失败：游戏文件夹不存在。GameId={game.Id}, FolderPath={game.FolderPath}");
                CustomMessageBox.Show($"游戏文件夹不存在：\n{game.FolderPath}\n\n记录会保留在数据库中，重新安装后可在详情页重新选择启动项。");
                return;
            }

            var candidates = await _viewModel.GameService.GetExecutableCandidatesAsync(game.Id);
            if (candidates.Count == 0)
            {
                _logger.Warn($"打开游戏失败：未找到可用 exe。GameId={game.Id}, FolderPath={game.FolderPath}");
                CustomMessageBox.Show($"没有在游戏目录中找到可用的 exe：\n{game.FolderPath}");
                return;
            }

            var selectedPath = candidates.Count == 1
                ? candidates[0]
                : ChooseGameExecutable(game, candidates);

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            var updated = await _viewModel.GameService.SetLaunchPathAsync(game.Id, selectedPath);
            if (updated?.LaunchPath == null)
            {
                CustomMessageBox.Show("启动项保存失败，请确认选择的是游戏目录内的 exe。");
                return;
            }

            game.LaunchPath = updated.LaunchPath;
            LaunchGame(updated.LaunchPath);
        }

        private async void GameCard_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: Game game })
            {
                return;
            }

            var detail = new GameDetailWindow(game, _viewModel.GameService, _logger)
            {
                Owner = this
            };
            detail.ShowDialog();
            await _viewModel.LoadGamesAsync();
        }

        private string? ChooseGameExecutable(Game game, IReadOnlyList<string> candidates)
        {
            var dialog = new ExecutableSelectionWindow(game.FolderPath, candidates)
            {
                Owner = this
            };

            return dialog.ShowDialog() == true ? dialog.SelectedPath : null;
        }

        private void LaunchGame(string launchPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = launchPath,
                    WorkingDirectory = Path.GetDirectoryName(launchPath) ?? string.Empty,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                _logger.Info($"游戏已启动：LaunchPath={launchPath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"启动游戏异常：LaunchPath={launchPath}", ex);
                CustomMessageBox.Show($"启动失败：\n{ex.Message}");
            }
        }
    }
}
