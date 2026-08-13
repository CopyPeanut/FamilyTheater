using FamilyTheater.Core.Enum;
using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
using LoginWindow.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive;

namespace LoginWindow.Models
{
    public class LoginModel : ReactiveObject
    {
        private readonly IUserService _userService;
        private readonly Func<HomeWindow> _homeWindowFactory;
        private readonly Func<RegisterWindow> _registerWindowFactory;
        private readonly IAppLogger _logger;

        public LoginModel(
            IUserService userService,
            Func<HomeWindow> homeWindowFactory,
            Func<RegisterWindow> registerWindowFactory,
            IAppLogger logger)
        {
            _userService = userService;
            _homeWindowFactory = homeWindowFactory;
            _registerWindowFactory = registerWindowFactory;
            _logger = logger;

            LoginCmd = ReactiveCommand.Create(OnLogin);
            RegisterCmd = ReactiveCommand.Create(OnRegister);
        }

        [Reactive] public string UserName { get; set; } = string.Empty;
        [Reactive] public string Password { get; set; } = string.Empty;

        public event Action? LoginSuccess;

        public ReactiveCommand<Unit, Unit> LoginCmd { get; }
        public ReactiveCommand<Unit, Unit> RegisterCmd { get; }

        private async void OnLogin()
        {
            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password))
            {
                _logger.Warn("用户尝试登录失败：用户名或密码为空。");
                CustomMessageBox.Show("用户名或密码不能为空", LogLevel.ERROR);
                return;
            }

            var isValid = await _userService.ValidateCredentialsAsync(UserName, Password);
            if (isValid)
            {
                _logger.Info($"用户登录成功：{UserName}");
                var homeWindow = _homeWindowFactory();
                homeWindow.Show();
                LoginSuccess?.Invoke();
            }
            else
            {
                _logger.Warn($"用户登录失败：{UserName}");
                CustomMessageBox.Show("密码或用户名错误", LogLevel.ERROR);
            }
        }

        private void OnRegister()
        {
            var registerWindow = _registerWindowFactory();
            registerWindow.Show();
        }
    }
}
