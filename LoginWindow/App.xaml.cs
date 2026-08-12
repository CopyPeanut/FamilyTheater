using FamilyTheater.Core.Data;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Services;
using CoreLogger = FamilyTheater.Core.Logger.Logger;
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
    public partial class App : System.Windows.Application
    {
        public static IHost AppHost { get; private set; } = null!;
        public App()
        {
            DispatcherUnhandledException += (_, args) =>
            {
                CoreLogger.Fatal("UI 线程发生未处理异常。", args.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                    CoreLogger.Fatal("应用程序域发生未处理异常。", exception);
                else
                    CoreLogger.Fatal($"应用程序域发生未处理异常：{args.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                CoreLogger.Error("后台任务发生未观察异常。", args.Exception);
                args.SetObserved();
            };
        }
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FamilyTheater");
            Directory.CreateDirectory(appDataDir);

            CoreLogger.Configure(Path.Combine(appDataDir, "logs"));
            CoreLogger.Info("FamilyTheater 启动。");

            var dbPath = Path.Combine(appDataDir, "FamilyTheater. db");
            // 一次性迁移：如果AppData 里没有数据库但程序目录下有旧库，自动复制过去
            var oldDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FamilyTheater. db");
            if (!File.Exists(dbPath) && File.Exists(oldDbPath))
            {
                File.Copy(oldDbPath, dbPath, overwrite: false);
                CoreLogger.Info($"已迁移旧数据库：{oldDbPath} -> {dbPath}");
            }

            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddCoreServices(dbPath);

                    services.AddTransient<LoginModel>();
                    services.AddTransient<Login>();
                    services.AddTransient<RegisterWindow>();
                    services.AddTransient<RegisterWindowModel>();
                    services.AddTransient<HomeWindow>();
                    services.AddTransient<HomeWindowModel>();
                    services.AddTransient<ConfigWindow>();
                    services.AddTransient<ConfigWindowModel>();

                    services.AddSingleton<Func<HomeWindow>>(provider => () => provider.GetRequiredService<HomeWindow>());
                    services.AddSingleton<Func<RegisterWindow>>(provider => () => provider.GetRequiredService<RegisterWindow>());
                    services.AddSingleton<Func<ConfigWindow>>(provider => () => provider.GetRequiredService<ConfigWindow>());

                })
                .Build();

            using (var scope = AppHost.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await Task.Run(() =>
                {
                    // 确保数据库文件存在
                    db.Database.EnsureCreated();

                    var script = db.Database.GenerateCreateScript();
                    var idempotentScript = script
                        .Replace("CREATE UNIQUE INDEX \"", "CREATE UNIQUE INDEX IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase)
                        .Replace("CREATE INDEX \"", "CREATE INDEX IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase)
                        .Replace("CREATE TABLE \"", "CREATE TABLE IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase);
                    db.Database.ExecuteSqlRaw(idempotentScript);

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
            loginViewModel.LoginSuccess += () =>
            {
                loginView.Close(); // 在这里关闭窗口
            };
            loginView.DataContext = loginViewModel;
            loginView.ViewModel = loginViewModel;
            loginView.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CoreLogger.Info("FamilyTheater 退出。");
            AppHost?.Dispose();
            base.OnExit(e);
        }
    }

}
