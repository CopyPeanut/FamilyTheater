using LoginWindow.Models;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Windows;

namespace LoginWindow.Views
{
    public partial class ChangePasswordWindow : Window, IViewFor<ChangePasswordWindowModel>
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(ChangePasswordWindowModel),
                typeof(ChangePasswordWindow),
                new PropertyMetadata(null));

        ChangePasswordWindowModel IViewFor<ChangePasswordWindowModel>.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value;
        }

        public ChangePasswordWindowModel ViewModel
        {
            get => (ChangePasswordWindowModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => throw new NotImplementedException();
        }

        public ChangePasswordWindow(ChangePasswordWindowModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            viewModel.PasswordChanged += Close;

            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel, vm => vm, v => v.DataContext)
                    .DisposeWith(disposables);
            });
        }
    }
}
