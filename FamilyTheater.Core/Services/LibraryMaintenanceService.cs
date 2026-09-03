using FamilyTheater.Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public class LibraryMaintenanceService : ILibraryMaintenanceService
{
    private readonly ILibraryDbContextFactory _dbContextFactory;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly IAppLogger _logger;

    public LibraryMaintenanceService(
        ILibraryDbContextFactory dbContextFactory,
        ICurrentUserSession currentUserSession,
        IAppLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _currentUserSession = currentUserSession;
        _logger = logger;
    }

    public async Task<ClearLibraryResult> ClearLibraryAsync()
    {
        if (!_currentUserSession.IsAdmin)
        {
            _logger.Warn($"清空媒体库失败：当前用户不是 admin。UserId={_currentUserSession.UserId}");
            throw new UnauthorizedAccessException("只有 admin 可以清空媒体库。");
        }

        using var db = _dbContextFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var result = new ClearLibraryResult
        {
            MovieTags = await db.MovieTags.ExecuteDeleteAsync(),
            PictureTags = await db.PictureTags.ExecuteDeleteAsync(),
            MangaTags = await db.MangaTags.ExecuteDeleteAsync(),
            GameTags = await db.GameTags.ExecuteDeleteAsync()
        };

        result.Movies = await db.Movies.ExecuteDeleteAsync();
        result.Pictures = await db.Pictures.ExecuteDeleteAsync();
        result.Mangas = await db.Mangas.ExecuteDeleteAsync();
        result.Games = await db.Games.ExecuteDeleteAsync();

        await transaction.CommitAsync();

        _logger.Warn(
            $"媒体库已清空：Movies={result.Movies}, MovieTags={result.MovieTags}, " +
            $"Pictures={result.Pictures}, PictureTags={result.PictureTags}, " +
            $"Mangas={result.Mangas}, MangaTags={result.MangaTags}, " +
            $"Games={result.Games}, GameTags={result.GameTags}, Db={_dbContextFactory.CurrentDatabasePath}");

        return result;
    }
}
