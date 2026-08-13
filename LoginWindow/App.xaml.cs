using FamilyTheater.Core.Data;
using FamilyTheater.Core.Helper;
using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
using LoginWindow.Models;
using LoginWindow.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace LoginWindow;

public partial class App : System.Windows.Application
{
    private IAppLogger? _logger;

    public static IHost AppHost { get; private set; } = null!;

    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.Fatal("UI 线程发生未处理异常。", args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                _logger?.Fatal("应用程序域发生未处理异常。", exception);
            }
            else
            {
                _logger?.Fatal($"应用程序域发生未处理异常：{args.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.Error("后台任务发生未观察异常。", args.Exception);
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

        var logDirectory = Path.Combine(appDataDir, "logs");
        var logger = new FileLogger(logDirectory);
        _logger = logger;
        logger.Info("FamilyTheater 启动。");

        var dbPath = Path.Combine(appDataDir, "FamilyTheater.db");
        var oldDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FamilyTheater.db");

        if (!File.Exists(dbPath) && File.Exists(oldDbPath))
        {
            File.Copy(oldDbPath, dbPath, overwrite: false);
            logger.Info($"已迁移旧数据库：{oldDbPath} -> {dbPath}");
        }

        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton(logger);
                services.AddSingleton<IAppLogger>(logger);

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

        loginViewModel.LoginSuccess += () => loginView.Close();
        loginView.DataContext = loginViewModel;
        loginView.ViewModel = loginViewModel;
        loginView.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info("FamilyTheater 退出。");
        AppHost?.Dispose();
        base.OnExit(e);
    }
}
