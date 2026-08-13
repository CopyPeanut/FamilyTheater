using System.ComponentModel.DataAnnotations;

namespace FamilyTheater.Core.Data;

/// <summary>
/// 一部电影。一个文件夹 = 一条记录。
/// </summary>
public class Movie
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 电影标题。默认用文件夹名，后续可在 UI 编辑。
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 电影文件夹的绝对路径（如 d:\movie\english\anime\superman）。
    /// 扫描时填入，用于定位视频和海报文件。
    /// </summary>
    [Required]
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>
    /// 视频文件的绝对路径（文件夹内第一个视频文件）。
    /// </summary>
    [Required]
    public string VideoFilePath { get; set; } = string.Empty;

    /// <summary>
    /// 海报文件路径。优先级：同目录图片 > FFmpeg 提取的缩略帧 > null（占位图）。
    /// null 时由服务层在运行时尝试提取。
    /// </summary>
    public string? PosterPath { get; set; }

    /// <summary>
    /// 年份，可选，后续手动编辑或联网补全。
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// 简介，可选。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 时长（秒），扫描时从视频元数据读取，可选。
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// 文件大小（字节），扫描时记录。
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// 最后扫描时间。
    /// </summary>
    public DateTime LastScannedAt { get; set; }

    /// <summary>
    /// 该电影的所有标签关联。
    /// </summary>
    public List<MovieTag> MovieTags { get; set; } = new();
}