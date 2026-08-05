using System.Reactive.Disposables;
using System.Windows;
using LoginWindow.Models;
using ReactiveUI;

namespace LoginWindow.Views
{
    public partial class HomeWindow : Window, IViewFor<HomeWindowModel>
    {

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(HomeWindowModel),
                typeof(HomeWindow),
                new PropertyMetadata(null));

        // 显式实现接口
        HomeWindowModel IViewFor<HomeWindowModel>.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value;
        }

        // 公共属性
        public HomeWindowModel ViewModel
        {
            get => (HomeWindowModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        object? IViewFor.ViewModel { get => ViewModel; set => throw new NotImplementedException(); }


        public HomeWindow()
        {
            InitializeComponent();
            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm, v => v.DataContext)
                    .DisposeWith(disposables);
            });
        }

        private void OpenRegisterWindow_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = new HomeWindow();
            registerWindow.Owner = this;
            HomeWindowModel model = new(ViewModel._dbContext);
            registerWindow.DataContext = model;
            registerWindow.ShowDialog();
        }
    }
}
