using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

public static class DatabaseSchemaMaintenance
{
    public static void EnsureMovieFileIndexes(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS \"IX_Movies_FolderPath\";");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_Movies_FolderPath\" ON \"Movies\" (\"FolderPath\");");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Movies_VideoFilePath\" ON \"Movies\" (\"VideoFilePath\");");
    }
}
