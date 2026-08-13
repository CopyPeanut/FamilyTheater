using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

/// <summary>
/// Movie 的标签。每条记录 = 一个标签关联。
/// 合并了标签字典和关联表，无需独立 Tag 表。
/// 未来 Manga/Game 各建同构的 MangaTag/GameTag 表。
/// </summary>
public class MovieTag
{
    [Key]
    public int Id { get; set; }

    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    /// <summary>
    /// 标签名，如"动漫""英语""动作"。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TagName { get; set; } = string.Empty;
}
