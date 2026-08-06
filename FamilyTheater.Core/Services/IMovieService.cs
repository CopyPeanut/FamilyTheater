using FamilyTheater.Core.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Services;

public interface IMovieService
{
    /// <summary>
    /// 扫描媒体库：遍历叶子文件夹 → 扫视频/海报 → 写库。
    /// 叶子文件夹 = 不含子目录的文件夹，每个叶子文件夹 = 一条 Movie 记录。
    /// 已存在的记录（按 FolderPath 匹配）更新信息，不重复新增。
    /// </summary>
    /// <returns>本次扫描新增 / 更新 / 跳过的电影数量。</returns>
    Task<ScanResult> ScanLibraryAsync();

    /// <summary>
    /// 获取全部电影（含标签），用于首页海报展示。
    /// </summary>
    Task<List<Movie>> GetAllMoviesAsync();
}

/// <summary>
/// 扫描结果统计。
/// </summary>
public class ScanResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
}
