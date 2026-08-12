using FamilyTheater.Core.Data;
using FamilyTheater.Core.Enum;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Services;
using HandyControl.Controls;      // MessageBox、Growl 等控件
using HandyControl.Data;
using LoginWindow.Views;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows;
namespace LoginWindow.Models
{
    public class RegisterWindowModel : ReactiveObject
    {
        IUserService _userService;
        public RegisterWindowModel(IUserService userService)
        {
            _userService = userService;
            RegisterCommand = ReactiveCommand.Create(OnRegister);
        }

        #region 属性
        [Reactive]
        public string UserName { get; set; }
        [Reactive]
        public string Password { get; set; }
        [Reactive]
        public string ConfirmPassword { get; set; }

        private bool IsRegistering { get; set; }
        #endregion

        #region command
        public ReactiveCommand<Unit, Unit> RegisterCommand { get; }
        private async void OnRegister()
        {
            if (IsRegistering) return;
            IsRegistering = true;
            try
            {
                if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(ConfirmPassword))
                {
                    CustomMessageBox.Show("注册信息不能为空", LogLevel.ERROR);
                    return;
                }
                // 1️⃣ 前端基础校验
                if (Password != ConfirmPassword)
                {
                    CustomMessageBox.Show("密码不一致，请检查",LogLevel.WARN);
                    Password = ConfirmPassword = "";
                    return;
                }

                if (Password.Length < 6)
                {
                    CustomMessageBox.Show("密码至少需六位", LogLevel.WARN);
                    Password = ConfirmPassword = "";
                    return;
                }

                if (await _userService.IsUserExistsAsync(UserName.Trim()))
                {
                    CustomMessageBox.Show("该用户名已被注册", LogLevel.WARN);
                    return;
                }

                var user = new User
                {
                    Username = UserName.Trim(),
                    PasswordHash = LoginHelper.HashPassword(Password)
                };

                var success = await _userService.RegisterAsync(UserName.Trim(), Password);
                CustomMessageBox.Show(success ? "注册成功！" : "注册失败，请重试");
            }
            finally
            {
                IsRegistering = false;
            }
        }

        #endregion
    }
}
