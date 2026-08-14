using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class LibraryDbContextFactory : ILibraryDbContextFactory
{
    private readonly string _managerDbPath;
    private readonly string _userDbPath;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly object _initLock = new();
    private readonly HashSet<string> _initializedDbPaths = new(StringComparer.OrdinalIgnoreCase);

    public LibraryDbContextFactory(
        string managerDbPath,
        string userDbPath,
        ICurrentUserSession currentUserSession)
    {
        _managerDbPath = managerDbPath;
        _userDbPath = userDbPath;
        _currentUserSession = currentUserSession;
    }

    public string CurrentDatabasePath =>
        _currentUserSession.Role == UserRoles.User ? _userDbPath : _managerDbPath;

    public AppDbContext CreateDbContext()
    {
        var dbPath = CurrentDatabasePath;
        EnsureDatabaseCreated(dbPath);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath};Cache=Shared")
            .Options;

        return new AppDbContext(options);
    }

    private void EnsureDatabaseCreated(string dbPath)
    {
        lock (_initLock)
        {
            if (_initializedDbPaths.Contains(dbPath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath};Cache=Shared")
                .Options;

            using var db = new AppDbContext(options);
            db.Database.EnsureCreated();
            _initializedDbPaths.Add(dbPath);
        }
    }
}
