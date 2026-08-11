namespace FamilyTheater.Core.Data;

/// <summary>
/// Picture 与 Tag 的多对多关联表。
/// 与 MovieTag 独立，复用同一个 Tag 表。
/// </summary>
public class PictureTag
{
    public int PictureId { get; set; }
    public Picture Picture { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
