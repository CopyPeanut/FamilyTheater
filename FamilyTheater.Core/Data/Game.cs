using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

public class Game
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string FolderPath { get; set; } = string.Empty;

    public string? LaunchPath { get; set; }

    public string? PosterPath { get; set; }

    public string? Description { get; set; }

    public long FolderSizeBytes { get; set; }

    public DateTime LastScannedAt { get; set; }

    public List<GameTag> GameTags { get; set; } = new();
}
