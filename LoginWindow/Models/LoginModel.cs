using FamilyTheater.Core.Data;
using FamilyTheater.Core.Enum;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Services;
using LoginWindow.Views;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Windows;
namespace LoginWindow.Models
{

    public class LoginModel : ReactiveObject
    {
        IUserService _userService;
        private readonly Func<HomeWindow> _homeWindowFactory;
        private readonly Func<RegisterWindow> _registerWindowFactory;
        public LoginModel(IUserService userService, Func<HomeWindow> homeWindowFactory, Func<RegisterWindow> registerWindowFactory)
        {
            _userService = userService;
            _homeWindowFactory = homeWindowFactory;
            _registerWindowFactory = registerWindowFactory;
            LoginCmd = ReactiveCommand.Create(OnLogin);
            RegisterCmd = ReactiveCommand.Create(OnRegister);
        }

        #region 属性
        [Reactive]
        public string UserName { get; set; }
        [Reactive]
        public string Password { get; set; }
        public event Action LoginSuccess;

        #endregion

        #region Command
        public ReactiveCommand<Unit, Unit> LoginCmd { get; }
        private async void OnLogin()
        {
            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password))
            {
                CustomMessageBox.Show("用户名或密码不能为空", LogLevel.ERROR);
                return;
                //var homeWindow = _homeWindowFactory();
                //homeWindow.Show();
                //LoginSuccess?.Invoke();
                //return;
            }
            var ok = await _userService.ValidateCredentialsAsync(UserName, Password);
            if (ok)
            {
                var homeWindow = _homeWindowFactory();
                homeWindow.Show();
                LoginSuccess?.Invoke();
            }
            else
            {
                CustomMessageBox.Show("密码或用户名错误！", LogLevel.ERROR);
            }
        }
        public ReactiveCommand<Unit, Unit> RegisterCmd { get; }

        private void OnRegister()
        {
            var registerWindow = _registerWindowFactory();
            registerWindow.Show();
        }
        #endregion
    }
}