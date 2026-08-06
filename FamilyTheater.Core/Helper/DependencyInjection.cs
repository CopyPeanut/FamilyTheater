using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.IO;
using FamilyTheater.Core.Data;
using System.Security.Cryptography;
namespace FamilyTheater.Core.Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services, string dbPath)
        {
            // 统一注册 DbContext
            services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath};Cache=Shared"),
                    ServiceLifetime.Scoped);

            // UserService 改为 Scoped
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ISettingService, SettingService>();
            services.AddScoped<IMovieService, MovieService>();

            return services;
        }
    }
}
