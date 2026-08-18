using FamilyTheater.Core.Data;

namespace FamilyTheater.Core.Services;

public interface IGameService
{
    Task<ScanResult> ScanLibraryAsync();

    Task<List<Game>> GetAllGamesAsync();

    Task<List<string>> GetAllTagsAsync();

    Task<Game?> GetGameByIdAsync(int gameId);

    Task<Game?> RenameGameAsync(int gameId, string newTitle);

    Task AddTagToGameAsync(int gameId, string tagName);

    Task RemoveTagFromGameAsync(int gameId, string tagName);

    Task DeleteTagAsync(string tagName);

    Task DeleteGameAsync(int gameId);

    Task<List<string>> GetExecutableCandidatesAsync(int gameId);

    Task<Game?> SetLaunchPathAsync(int gameId, string launchPath);
}
