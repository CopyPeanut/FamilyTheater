namespace FamilyTheater.Core.Services;

public interface ILibraryMaintenanceService
{
    Task<ClearLibraryResult> ClearLibraryAsync();
}

public class ClearLibraryResult
{
    public int Movies { get; set; }
    public int MovieTags { get; set; }
    public int Pictures { get; set; }
    public int PictureTags { get; set; }
    public int Mangas { get; set; }
    public int MangaTags { get; set; }
    public int Games { get; set; }
    public int GameTags { get; set; }

    public int TotalItems => Movies + Pictures + Mangas + Games;
    public int TotalTags => MovieTags + PictureTags + MangaTags + GameTags;
}
