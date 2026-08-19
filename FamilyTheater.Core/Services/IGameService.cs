using FamilyTheater.Core.Data;

namespace FamilyTheater.Core.Services;

public interface IGameService
{
    Task<ScanResult> ScanLibraryAsync();

    Task<List<Game>> GetAllGamesAsync();

    Task<List<string>> GetAllTagsAsync();

    Task<Game?> GetGameByIdAsync(int gameId);

    Task<Game?> RenameGameAsync(int gameId, string newTitle);

    Task<Game?> SetPosterPathAsync(int gameId, string posterPath);

    Task AddTagToGameAsync(int gameId, string tagName);

    Task RemoveTagFromGameAsync(int gameId, string tagName);

    Task DeleteTagAsync(string tagName);

    Task DeleteGameAsync(int gameId);

    Task<List<string>> GetExecutableCandidatesAsync(int gameId);

    Task<Game?> SetLaunchPathAsync(int gameId, string launchPath);

    Task<Game?> SetScreenshotRootPathAsync(int gameId, string screenshotRootPath);

    Task<List<string>> GetScreenshotImagesAsync(int gameId);
}
