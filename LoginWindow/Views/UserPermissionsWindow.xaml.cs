using LoginWindow.Models;
using ReactiveUI;
using System;
using System.Windows;

namespace LoginWindow.Views
{
    public partial class UserPermissionsWindow : Window, IViewFor<UserPermissionsWindowModel>
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(UserPermissionsWindowModel),
                typeof(UserPermissionsWindow),
                new PropertyMetadata(null));

        UserPermissionsWindowModel IViewFor<UserPermissionsWindowModel>.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value;
        }

        public UserPermissionsWindowModel ViewModel
        {
            get => (UserPermissionsWindowModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => throw new NotImplementedException();
        }

        public UserPermissionsWindow(UserPermissionsWindowModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            ViewModel = viewModel;
        }
    }
}
