using FamilyTheater.Core.Enum;
using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
using LoginWindow.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;

namespace LoginWindow.Models
{
    public class RegisterWindowModel : ReactiveObject
    {
        private readonly IUserService _userService;
        private readonly IAppLogger _logger;

        public RegisterWindowModel(IUserService userService, IAppLogger logger)
        {
            _userService = userService;
            _logger = logger;
            RegisterCommand = ReactiveCommand.Create(OnRegister);
        }

        [Reactive] public string UserName { get; set; } = string.Empty;
        [Reactive] public string Password { get; set; } = string.Empty;
        [Reactive] public string ConfirmPassword { get; set; } = string.Empty;

        private bool IsRegistering { get; set; }

        public ReactiveCommand<Unit, Unit> RegisterCommand { get; }

        private async void OnRegister()
        {
            if (IsRegistering)
            {
                return;
            }

            IsRegistering = true;

            try
            {
                if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(ConfirmPassword))
                {
                    _logger.Warn("用户注册失败：注册信息为空。");
                    CustomMessageBox.Show("注册信息不能为空", LogLevel.ERROR);
                    return;
                }

                if (Password != ConfirmPassword)
                {
                    _logger.Warn($"用户注册失败：两次密码不一致。UserName={UserName}");
                    CustomMessageBox.Show("密码不一致，请检查", LogLevel.WARN);
                    Password = string.Empty;
                    ConfirmPassword = string.Empty;
                    return;
                }

                if (Password.Length < 6)
                {
                    _logger.Warn($"用户注册失败：密码长度不足。UserName={UserName}");
                    CustomMessageBox.Show("密码至少需要六位", LogLevel.WARN);
                    Password = string.Empty;
                    ConfirmPassword = string.Empty;
                    return;
                }

                var normalizedUserName = UserName.Trim();
                if (await _userService.IsUserExistsAsync(normalizedUserName))
                {
                    _logger.Warn($"用户注册失败：用户名已存在。UserName={normalizedUserName}");
                    CustomMessageBox.Show("该用户名已被注册", LogLevel.WARN);
                    return;
                }

                var success = await _userService.RegisterAsync(normalizedUserName, Password);
                _logger.Log(
                    success ? LogLevel.INFO : LogLevel.ERROR,
                    success ? $"用户注册成功：{normalizedUserName}" : $"用户注册失败：{normalizedUserName}");

                CustomMessageBox.Show(success ? "注册成功" : "注册失败，请重试");
            }
            finally
            {
                IsRegistering = false;
            }
        }
    }
}
