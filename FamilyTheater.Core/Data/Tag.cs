using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

/// <summary>
/// 标签。用于筛选和搜索。一个电影可有多个标签。
/// </summary>
public class Tag
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 标签名，如"动漫""英语""动作"。唯一。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 导航属性。
    /// </summary>
    public List<MovieTag> MovieTags { get; set; } = new();

    /// <summary>
    /// Picture 多对多导航属性。
    /// </summary>
    public List<PictureTag> PictureTags { get; set; } = new();
}