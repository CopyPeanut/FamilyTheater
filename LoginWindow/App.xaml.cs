using FamilyTheater.Core.Data;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Services;
using LoginWindow.Models;
using LoginWindow.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

namespace LoginWindow
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IHost AppHost { get; private set; } = null!;
        public App()
        {
         
        }
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var dbPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "FamilyTheater.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddCoreServices(dbPath);

                    services.AddTransient<LoginModel>();
                    services.AddTransient<Login>();
                    services.AddTransient<RegisterWindow>();
                    services.AddTransient<RegisterWindowModel>();
                })
                .Build();

            using (var scope = AppHost.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await Task.Run(() =>
                {
                    db.Database.EnsureCreated();
                    if (!db.Users.Any())
                    {
                        db.Users.Add(new User
                        {
                            Username = "admin",
                            PasswordHash = LoginHelper.HashPassword("123456")
                        });
                        db.SaveChanges();
                    }
                });
            }
            var loginViewModel = AppHost.Services.GetRequiredService<LoginModel>();
            var loginView = AppHost.Services.GetRequiredService<Login>();
            loginView.DataContext = loginViewModel;
            loginView.ViewModel = loginViewModel;
            loginView.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppHost?.Dispose();
            base.OnExit(e);
        }
    }

}
