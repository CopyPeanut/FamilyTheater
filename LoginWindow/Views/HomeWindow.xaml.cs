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
        private void MovieCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Movie movie)
            {
                var player = new PlayerWindow(movie.VideoFilePath);
                player.Owner = this;
                player.Show();
            }
        }

        /// <summary>
        /// 电影卡片右键点击 → 打开详情弹窗（管理标签）。
        /// </summary>
        private void MovieCard_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Movie movie)
            {
                var detail = new MovieDetailWindow(movie, _viewModel.MovieService);
                detail.Owner = this;
                detail.ShowDialog();
                // 关闭后刷新海报列表（标签可能在详情里改过）
                _ = _viewModel.LoadMoviesAsync();
            }
        }
    }
}
