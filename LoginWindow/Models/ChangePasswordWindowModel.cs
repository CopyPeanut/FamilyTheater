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
    public class ChangePasswordWindowModel : ReactiveObject
    {
        private readonly IUserService _userService;
        private readonly IAppLogger _logger;

        [Reactive] public string OldPassword { get; set; } = string.Empty;
        [Reactive] public string NewPassword { get; set; } = string.Empty;
        [Reactive] public string ConfirmPassword { get; set; } = string.Empty;
        [Reactive] public bool IsSaving { get; set; }

        public event Action? PasswordChanged;

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public ChangePasswordWindowModel(IUserService userService, IAppLogger logger)
        {
            _userService = userService;
            _logger = logger;

            SaveCommand = ReactiveCommand.Create(OnSave);
        }

        private async void OnSave()
        {
            if (IsSaving)
            {
                return;
            }

            IsSaving = true;
            try
            {
                if (string.IsNullOrWhiteSpace(OldPassword) ||
                    string.IsNullOrWhiteSpace(NewPassword) ||
                    string.IsNullOrWhiteSpace(ConfirmPassword))
                {
                    CustomMessageBox.Show("密码信息不能为空", LogLevel.ERROR);
                    return;
                }

                if (NewPassword != ConfirmPassword)
                {
                    CustomMessageBox.Show("两次输入的新密码不一致", LogLevel.WARN);
                    NewPassword = string.Empty;
                    ConfirmPassword = string.Empty;
                    return;
                }

                if (NewPassword.Length < 6)
                {
                    CustomMessageBox.Show("新密码至少需要 6 位", LogLevel.WARN);
                    NewPassword = string.Empty;
                    ConfirmPassword = string.Empty;
                    return;
                }

                var success = await _userService.ChangeCurrentUserPasswordAsync(OldPassword, NewPassword);
                if (!success)
                {
                    CustomMessageBox.Show("修改失败，请检查旧密码是否正确", LogLevel.ERROR);
                    OldPassword = string.Empty;
                    return;
                }

                _logger.Info("当前用户密码修改成功。");
                CustomMessageBox.Show("密码修改成功");
                PasswordChanged?.Invoke();
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
