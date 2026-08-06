using FamilyTheater.Core.Data;
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

            // 窗口 Loaded 事件触发时加载电影数据（确保 DataContext 已绑定、控件已初始化）
            this.Loaded += HomeWindow_Loaded;
        }

        private async void HomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 窗口完全加载后从数据库加载电影
            await _viewModel.LoadMoviesAsync();
        }

        /// <summary>
        /// 电影卡片点击 → 打开播放弹窗。
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
    }
}
