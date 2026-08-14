using FamilyTheater.Core.Data;

namespace FamilyTheater.Core.Services;

public interface ILibraryDbContextFactory
{
    string CurrentDatabasePath { get; }
    AppDbContext CreateDbContext();
}
