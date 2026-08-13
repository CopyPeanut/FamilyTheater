using FamilyTheater.Core.Data;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IAppLogger _logger;

    public UserService(AppDbContext db, IAppLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> ValidateCredentialsAsync(string userName, string password)
    {
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            _logger.Warn("登录校验失败：用户名或密码为空。");
            return false;
        }

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Username == userName)
            .Select(u => new { u.PasswordHash })
            .FirstOrDefaultAsync();

        var isValid = user != null && LoginHelper.VerifyPassword(password, user.PasswordHash);
        if (isValid)
            _logger.Info($"用户登录校验成功：{userName}");
        else
            _logger.Warn($"用户登录校验失败：{userName}");

        return isValid;
    }

    public async Task<bool> RegisterAsync(string userName, string password)
    {
        if (await IsUserExistsAsync(userName))
        {
            _logger.Warn($"用户注册失败：用户名已存在。UserName={userName}");
            return false;
        }

        _db.Users.Add(new User
        {
            Username = userName,
            PasswordHash = LoginHelper.HashPassword(password)
        });

        await _db.SaveChangesAsync();
        _logger.Info($"用户注册成功：{userName}");
        return true;
    }

    public async Task<bool> IsUserExistsAsync(string userName)
    {
        return await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Username == userName);
    }
}
