using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

/// <summary>
/// Picture 的标签。每条记录 = 一个标签关联。
/// 合并了标签字典和关联表，无需独立 Tag 表。
/// 与 MovieTag 完全独立，互不影响。
/// </summary>
public class PictureTag
{
    [Key]
    public int Id { get; set; }

    public int PictureId { get; set; }
    public Picture Picture { get; set; } = null!;

    /// <summary>
    /// 标签名。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TagName { get; set; } = string.Empty;
}
