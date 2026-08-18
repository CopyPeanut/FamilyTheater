using FamilyTheater.Core.Data;
using FamilyTheater.Core.Logger;
using FamilyTheater.Core.Services;
using LoginWindow.Models;
using LoginWindow.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Data;
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
        var userDbPath = Path.Combine(appDataDir, "FamilyTheater.User.db");
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

                services.AddCoreServices(dbPath, userDbPath);

                services.AddTransient<LoginModel>();
                services.AddTransient<Login>();
                services.AddTransient<RegisterWindow>();
                services.AddTransient<RegisterWindowModel>();
                services.AddTransient<HomeWindow>();
                services.AddTransient<HomeWindowModel>();
                services.AddTransient<ConfigWindow>();
                services.AddTransient<ConfigWindowModel>();
                services.AddTransient<UserPermissionsWindow>();
                services.AddTransient<UserPermissionsWindowModel>();
                services.AddTransient<ChangePasswordWindow>();
                services.AddTransient<ChangePasswordWindowModel>();

                services.AddSingleton<Func<HomeWindow>>(provider => () => provider.GetRequiredService<HomeWindow>());
                services.AddSingleton<Func<RegisterWindow>>(provider => () => provider.GetRequiredService<RegisterWindow>());
                services.AddSingleton<Func<ConfigWindow>>(provider => () => provider.GetRequiredService<ConfigWindow>());
                services.AddSingleton<Func<UserPermissionsWindow>>(provider => () => provider.GetRequiredService<UserPermissionsWindow>());
                services.AddSingleton<Func<ChangePasswordWindow>>(provider => () => provider.GetRequiredService<ChangePasswordWindow>());
                services.AddSingleton<Func<Login>>(provider => () => provider.GetRequiredService<Login>());
            })
            .Build();

        using (var scope = AppHost.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await Task.Run(() =>
            {
                db.Database.EnsureCreated();

                DatabaseSchemaMaintenance.EnsureCoreTables(db);
                EnsureUserRoleColumn(db);
                DatabaseSchemaMaintenance.EnsureMovieFileIndexes(db);
                DatabaseSchemaMaintenance.EnsureGameIndexes(db);

                if (db.Users.Any() && !db.Users.Any(user => user.Role == UserRoles.Admin))
                {
                    var firstUser = db.Users.OrderBy(user => user.Id).First();
                    firstUser.Role = UserRoles.Admin;
                    db.SaveChanges();
                    logger.Info($"未发现 admin 账户，已将首个用户设为 admin：UserId={firstUser.Id}，Username={firstUser.Username}");
                }
            });
        }

        var loginView = AppHost.Services.GetRequiredService<Login>();
        loginView.Show();
    }

    private static void EnsureUserRoleColumn(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(\"Users\");";
            using var reader = command.ExecuteReader();

            var hasRoleColumn = false;
            while (reader.Read())
            {
                if (string.Equals(reader["name"]?.ToString(), nameof(User.Role), StringComparison.OrdinalIgnoreCase))
                {
                    hasRoleColumn = true;
                    break;
                }
            }

            reader.Close();

            if (!hasRoleColumn)
            {
                db.Database.ExecuteSqlRaw($"ALTER TABLE \"Users\" ADD COLUMN \"Role\" TEXT NOT NULL DEFAULT '{UserRoles.User}';");
            }
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Info("FamilyTheater 退出。");
        AppHost?.Dispose();
        base.OnExit(e);
    }
}
