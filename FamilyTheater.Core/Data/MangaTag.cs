using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FamilyTheater.Core.Data;

public class MangaTag
{
    [Key]
    public int Id { get; set; }

    public int MangaId { get; set; }

    [ForeignKey(nameof(MangaId))]
    public Manga Manga { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string TagName { get; set; } = string.Empty;
}
