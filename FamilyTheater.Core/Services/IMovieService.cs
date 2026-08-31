using FamilyTheater.Core.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Services;

public interface IMovieService
{
    /// <summary>
    /// 扫描媒体库。默认执行增量扫描，已按视频路径入库的记录会直接跳过；
    /// 完整扫描会重新检查已有视频及其海报。
    /// </summary>
    /// <param name="fullRescan">是否重新处理数据库中已有的视频。</param>
    /// <returns>本次扫描新增 / 更新 / 跳过的电影数量。</returns>
    Task<ScanResult> ScanLibraryAsync(bool fullRescan = false);

    /// <summary>
    /// 获取全部电影（含标签），用于首页海报展示。
    /// </summary>
    Task<List<Movie>> GetAllMoviesAsync();

    /// <summary>
    /// 获取全部电影标签名（DISTINCT），用于首页标签筛选。
    /// </summary>
    Task<List<string>> GetAllTagsAsync();

    /// <summary>
    /// 给电影添加标签（已存在则忽略），如果 Tag 表里没有该名称则先创建。
    /// </summary>
    Task AddTagToMovieAsync(int movieId, string tagName);

    /// <summary>
    /// 从电影移除指定标签关联（不删除 Tag 记录本身）。
    /// </summary>
    Task RemoveTagFromMovieAsync(int movieId, string tagName);

    Task DeleteTagAsync(string tagName);

    /// <summary>
    /// 获取单个电影（含标签），用于详情弹窗。
    /// </summary>
    Task<Movie?> GetMovieByIdAsync(int movieId);

    /// <summary>
    /// 重命名电影：改标题 + 改文件夹名 + 更新 FolderPath/VideoFilePath/PosterPath + 写库。
    /// 返回更新后的 Movie（路径已更新），失败返回 null。
    /// </summary>
    Task<Movie?> RenameMovieAsync(int movieId, string newTitle);

    Task<Movie?> SetPosterPathAsync(int movieId, string posterPath);

    /// <summary>
    /// 删除电影记录及其所有标签关联（MovieTag），不删除 Tag 记录本身。
    /// </summary>
    Task DeleteMovieAsync(int movieId);
}

/// <summary>
/// 扫描结果统计。
/// </summary>
public class ScanResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int PosterFailed { get; set; }

}
