using FamilyTheater.Core.Data;
using FamilyTheater.Core.Enum;
using FamilyTheater.Core.Helper;
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
        public readonly AppDbContext _dbContext;
        public LoginModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            LoginCmd = ReactiveCommand.Create(OnLogin);
        }

        #region 属性
        [Reactive]
        public string UserName { get; set; }
        [Reactive]
        public string Password { get; set; }


        #endregion

        #region Command
        public ReactiveCommand<Unit, Unit> LoginCmd { get; }
        private async void OnLogin()
        {
            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password))
            {
                CustomMessageBox.Show("用户名或密码不能为空",LogLevel.ERROR);
                return;
            }
            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Username == UserName)
                .Select(u => new { u.Id, u.PasswordHash })
                .FirstOrDefaultAsync();

            if (user != null && LoginHelper.VerifyPassword(Password, user.PasswordHash))
            {

                CustomMessageBox.Show("登录成功");

            }
            else
            {
                CustomMessageBox.Show("密码或用户名错误！",LogLevel.ERROR);
            }
            #endregion
        }
    }
}