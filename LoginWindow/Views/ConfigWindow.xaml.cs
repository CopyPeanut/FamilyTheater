using System.Reactive.Disposables;
using System.Windows;
using LoginWindow.Models;
using ReactiveUI;

namespace LoginWindow.Views
{
    public partial class ConfigWindow : Window, IViewFor<ConfigWindowModel>
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(ConfigWindowModel),
                typeof(ConfigWindow),
                new PropertyMetadata(null));

        ConfigWindowModel IViewFor<ConfigWindowModel>.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value;
        }

        public ConfigWindowModel ViewModel
        {
            get => (ConfigWindowModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel { get => ViewModel; set => throw new NotImplementedException(); }

        public ConfigWindow(ConfigWindowModel viewModel)
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