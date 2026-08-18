using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyTheater.Core.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services, string managerDbPath, string userDbPath)
    {
        services.AddDbContext<AppDbContext>(
            options => options.UseSqlite($"Data Source={managerDbPath};Cache=Shared"),
            ServiceLifetime.Scoped);

        services.AddSingleton<ICurrentUserSession, CurrentUserSession>();
        services.AddSingleton<ILibraryDbContextFactory>(provider =>
            new LibraryDbContextFactory(
                managerDbPath,
                userDbPath,
                provider.GetRequiredService<ICurrentUserSession>()));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<IPictureService, PictureService>();
        services.AddScoped<IGameService, GameService>();

        return services;
    }
}
