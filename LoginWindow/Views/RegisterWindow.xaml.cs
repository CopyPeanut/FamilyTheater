using LoginWindow.Models;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows;

namespace LoginWindow.Views
{
    /// <summary>
    /// RegisterWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RegisterWindow : Window, IViewFor<RegisterWindowModel>
    {

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(RegisterWindowModel),
                typeof(RegisterWindow),
                new PropertyMetadata(null));

        // 显式实现接口
        RegisterWindowModel IViewFor<RegisterWindowModel>.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value;
        }

        // 公共属性
        public RegisterWindowModel ViewModel
        {
            get => (RegisterWindowModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        object? IViewFor.ViewModel { get => ViewModel; set => throw new NotImplementedException(); }


        public RegisterWindow(RegisterWindowModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm, v => v.DataContext)
                    .DisposeWith(disposables);
            });
        }


    }
}
