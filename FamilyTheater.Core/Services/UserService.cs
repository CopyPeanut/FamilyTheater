// Services/UserService.cs
using FamilyTheater.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using FamilyTheater.Core.Helper;

namespace FamilyTheater.Core.Services;

public class UserService : IUserService
{
    private readonly IServiceProvider _serviceProvider;

    public UserService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ValidateCredentialsAsync(string userName, string password)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await Task.Run(() =>
            db.Users.FirstOrDefault(u => u.Username == userName));

        return user != null && LoginHelper.VerifyPassword(password, user.PasswordHash);
    }
}