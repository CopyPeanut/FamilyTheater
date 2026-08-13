using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

/// <summary>
/// 全局配置项。键值对存储，如媒体根目录、缩略图缓存目录等。
/// </summary>
public class Setting
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;
}