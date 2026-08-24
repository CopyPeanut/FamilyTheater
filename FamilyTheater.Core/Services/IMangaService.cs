using FamilyTheater.Core.Data;

namespace FamilyTheater.Core.Services;

public interface IMangaService
{
    Task<ScanResult> ScanLibraryAsync();

    Task<List<Manga>> GetAllMangasAsync();

    Task<List<string>> GetAllTagsAsync();

    Task<Manga?> GetMangaByIdAsync(int mangaId);

    Task<Manga?> RenameMangaAsync(int mangaId, string newTitle);

    Task<Manga?> SetPosterPathAsync(int mangaId, string posterPath);

    Task AddTagToMangaAsync(int mangaId, string tagName);

    Task RemoveTagFromMangaAsync(int mangaId, string tagName);

    Task DeleteTagAsync(string tagName);

    Task DeleteMangaAsync(int mangaId);
}
