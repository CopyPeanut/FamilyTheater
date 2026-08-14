using FamilyTheater.Core.Data;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IAppLogger _logger;
    private readonly ICurrentUserSession _currentUserSession;

    public UserService(AppDbContext db, IAppLogger logger, ICurrentUserSession currentUserSession)
    {
        _db = db;
        _logger = logger;
        _currentUserSession = currentUserSession;
    }

    public async Task<bool> ValidateCredentialsAsync(string userName, string password)
    {
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            _logger.Warn("登录校验失败：用户名或密码为空。");
            return false;
        }

        var normalizedUserName = userName.Trim();
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Username == normalizedUserName)
            .Select(u => new { u.Id, u.Username, u.PasswordHash, u.Role })
            .FirstOrDefaultAsync();

        var isValid = user != null && LoginHelper.VerifyPassword(password, user.PasswordHash);
        if (isValid)
        {
            _currentUserSession.SetCurrentUser(user!.Id, user.Username, user.Role);
            _logger.Info($"用户登录校验成功：{normalizedUserName}，Role={user.Role}");
        }
        else
        {
            _logger.Warn($"用户登录校验失败：{normalizedUserName}");
        }

        return isValid;
    }

    public async Task<bool> RegisterAsync(string userName, string password)
    {
        var normalizedUserName = userName.Trim();
        if (await IsUserExistsAsync(normalizedUserName))
        {
            _logger.Warn($"用户注册失败：用户名已存在。UserName={normalizedUserName}");
            return false;
        }

        var role = await _db.Users.AnyAsync(u => u.Role == UserRoles.Admin)
            ? UserRoles.User
            : UserRoles.Admin;

        _db.Users.Add(new User
        {
            Username = normalizedUserName,
            PasswordHash = LoginHelper.HashPassword(password),
            Role = role
        });

        await _db.SaveChangesAsync();
        _logger.Info($"用户注册成功：{normalizedUserName}，Role={role}");
        return true;
    }

    public async Task<bool> IsUserExistsAsync(string userName)
    {
        var normalizedUserName = userName.Trim();
        return await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Username == normalizedUserName);
    }

    public async Task<List<UserSummary>> GetAllUsersAsync()
    {
        if (!_currentUserSession.IsAdmin)
        {
            _logger.Warn($"获取用户列表失败：当前用户不是 admin。UserId={_currentUserSession.UserId}");
            return new List<UserSummary>();
        }

        return await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new UserSummary
            {
                Id = u.Id,
                Username = u.Username,
                Role = u.Role
            })
            .ToListAsync();
    }

    public async Task<bool> UpdateUserRoleAsync(int userId, string role)
    {
        if (!_currentUserSession.IsAdmin)
        {
            _logger.Warn($"修改用户权限失败：当前用户不是 admin。UserId={_currentUserSession.UserId}");
            return false;
        }

        var normalizedRole = UserRoles.Normalize(role);
        if (!UserRoles.IsEditableRole(normalizedRole))
        {
            _logger.Warn($"修改用户权限失败：角色不允许。TargetUserId={userId}，Role={role}");
            return false;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            _logger.Warn($"修改用户权限失败：用户不存在。TargetUserId={userId}");
            return false;
        }

        if (user.Role == UserRoles.Admin)
        {
            _logger.Warn($"修改用户权限失败：不允许在此界面修改 admin 用户。TargetUserId={userId}");
            return false;
        }

        user.Role = normalizedRole;
        await _db.SaveChangesAsync();
        _logger.Info($"用户权限已更新：TargetUserId={userId}，Role={normalizedRole}");
        return true;
    }

    public async Task<bool> ChangeCurrentUserPasswordAsync(string oldPassword, string newPassword)
    {
        if (!_currentUserSession.UserId.HasValue)
        {
            _logger.Warn("修改密码失败：当前没有登录用户。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            _logger.Warn($"修改密码失败：密码为空。UserId={_currentUserSession.UserId}");
            return false;
        }

        if (newPassword.Length < 6)
        {
            _logger.Warn($"修改密码失败：新密码长度不足。UserId={_currentUserSession.UserId}");
            return false;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUserSession.UserId.Value);
        if (user == null)
        {
            _logger.Warn($"修改密码失败：用户不存在。UserId={_currentUserSession.UserId}");
            return false;
        }

        if (!LoginHelper.VerifyPassword(oldPassword, user.PasswordHash))
        {
            _logger.Warn($"修改密码失败：旧密码不正确。UserId={_currentUserSession.UserId}");
            return false;
        }

        user.PasswordHash = LoginHelper.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        _logger.Info($"用户密码已修改：UserId={_currentUserSession.UserId}");
        return true;
    }

    public void Logout()
    {
        _logger.Info($"用户退出登录：UserId={_currentUserSession.UserId}，Username={_currentUserSession.Username}");
        _currentUserSession.Clear();
    }
}
