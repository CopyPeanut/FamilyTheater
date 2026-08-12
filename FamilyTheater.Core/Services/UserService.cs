using FamilyTheater.Core.Data;
using FamilyTheater.Core.Helper;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> ValidateCredentialsAsync(string userName, string password)
    {
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            return false;

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Username == userName)
            .Select(u => new { u.PasswordHash })
            .FirstOrDefaultAsync();

        return user != null && LoginHelper.VerifyPassword(password, user.PasswordHash);
    }

    public async Task<bool> RegisterAsync(string userName, string password)
    {
        if (await IsUserExistsAsync(userName))
            return false;

        _db.Users.Add(new User
        {
            Username = userName,
            PasswordHash = LoginHelper.HashPassword(password)
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsUserExistsAsync(string userName)
    {
        return await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Username == userName);
    }
}