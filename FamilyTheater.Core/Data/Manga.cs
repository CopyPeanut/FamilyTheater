using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

public class Manga
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public string FolderPath { get; set; } = string.Empty;

    public string? PosterPath { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTime LastScannedAt { get; set; }

    public List<MangaTag> MangaTags { get; set; } = new();
}
