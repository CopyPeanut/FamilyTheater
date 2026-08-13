using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyTheater.Core.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services, string dbPath)
    {
        services.AddDbContext<AppDbContext>(
            options => options.UseSqlite($"Data Source={dbPath};Cache=Shared"),
            ServiceLifetime.Scoped);

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<IPictureService, PictureService>();

        return services;
    }
}
