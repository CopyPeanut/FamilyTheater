using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
        EnsureColumn(db, "Games", "ScreenshotRootPath", "TEXT");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Games_FolderPath\" ON \"Games\" (\"FolderPath\");");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_Games_Title\" ON \"Games\" (\"Title\");");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_GameTags_GameId_TagName\" ON \"GameTags\" (\"GameId\", \"TagName\");");
    }

    private static void EnsureColumn(AppDbContext db, string tableName, string columnName, string columnDefinition)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }

        var sql = string.Concat(
            "ALTER TABLE ",
            QuoteIdentifier(tableName),
            " ADD COLUMN ",
            QuoteIdentifier(columnName),
            " ",
            columnDefinition,
            ";");
        db.Database.ExecuteSqlRaw(sql);
    }

    private static string QuoteIdentifier(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
