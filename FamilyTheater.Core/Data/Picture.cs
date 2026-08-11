using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

/// <summary>
/// 一张图片。一个图片文件 = 一条记录。
/// 图片按子文件夹分类，子文件夹名作为标签。
/// </summary>
public class Picture
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 图片文件绝对路径（唯一）。
    /// </summary>
    [Required]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件名（不含扩展名），用于详情页显示。展示页不显示。
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 所属子文件夹路径（PictureRootPath 下的第一层子文件夹）。
    /// 子文件夹名作为标签。
    /// </summary>
    [Required]
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）。
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// 最后扫描时间。
    /// </summary>
    public DateTime LastScannedAt { get; set; }

    /// <summary>
    /// 多对多导航属性。
    /// </summary>
    public List<PictureTag> PictureTags { get; set; } = new();
}
