using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

public class GameTag
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string TagName { get; set; } = string.Empty;
}
