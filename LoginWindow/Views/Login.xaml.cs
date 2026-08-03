using System.Reactive.Disposables;
using System.Windows;
using LoginWindow.Models;
using ReactiveUI;

namespace LoginWindow.Views
{
    public partial class Login : Window, IViewFor<LoginModel>
    {

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(LoginModel),
                typeof(Login),
                new PropertyMetadata(null));

        // 显式实现接口
        LoginModel IViewFor<LoginModel>.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value;
        }

        // 公共属性
        public LoginModel ViewModel
        {
            get => (LoginModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        object? IViewFor.ViewModel { get => ViewModel; set => throw new NotImplementedException(); }


        public Login()
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
            var registerWindow = new RegisterWindow();
            registerWindow.Owner = this;
            RegisterWindowModel model = new(ViewModel._dbContext);
            registerWindow.DataContext = model;
            registerWindow.ShowDialog();
        }
    }
}