namespace FamilyTheater.Core.Data;

/// <summary>
/// Movie 与 Tag 的多对多关联表。
/// </summary>
public class MovieTag
{
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}