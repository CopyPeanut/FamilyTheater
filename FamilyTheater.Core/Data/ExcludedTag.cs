using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

public class ExcludedTag
{
    [Key]
    public int Id { get; set; }

    public int Category { get; set; }

    [Required]
    [MaxLength(100)]
    public string TagName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string NormalizedTagName { get; set; } = string.Empty;
}
