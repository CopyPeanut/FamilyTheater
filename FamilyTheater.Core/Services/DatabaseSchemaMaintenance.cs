using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public static class DatabaseSchemaMaintenance
{
    public static void EnsureCoreTables(AppDbContext db)
    {
        var script = db.Database.GenerateCreateScript();
        var idempotentScript = script
            .Replace("CREATE UNIQUE INDEX \"", "CREATE UNIQUE INDEX IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase)
            .Replace("CREATE INDEX \"", "CREATE INDEX IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase)
            .Replace("CREATE TABLE \"", "CREATE TABLE IF NOT EXISTS \"", StringComparison.OrdinalIgnoreCase);

        db.Database.ExecuteSqlRaw(idempotentScript);
    }

    public static void EnsureMovieFileIndexes(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS \"IX_Movies_FolderPath\";");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_Movies_FolderPath\" ON \"Movies\" (\"FolderPath\");");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Movies_VideoFilePath\" ON \"Movies\" (\"VideoFilePath\");");
    }

    public static void EnsureGameIndexes(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Games_FolderPath\" ON \"Games\" (\"FolderPath\");");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_Games_Title\" ON \"Games\" (\"Title\");");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_GameTags_GameId_TagName\" ON \"GameTags\" (\"GameId\", \"TagName\");");
    }
}
