using FamilyTheater.Core.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Services;

public interface IPictureService
{
    /// <summary>
    /// 扫描图片库：遍历 PictureRootPath 下的子文件夹 → 扫图片文件 → 写库。
    /// 子文件夹名作为标签，图片文件本身没有独立文件夹。
    /// 已存在的记录（按 FilePath 匹配）更新信息，不重复新增。
    /// </summary>
    Task<ScanResult> ScanLibraryAsync(bool fullRescan = false);

    /// <summary>
    /// 获取全部图片（含标签），用于首页展示。
    /// </summary>
    Task<List<Picture>> GetAllPicturesAsync();

    /// <summary>
    /// 获取全部图片标签名（DISTINCT），用于首页标签筛选。
    /// </summary>
    Task<List<string>> GetAllTagsAsync();

    /// <summary>
    /// 给图片添加标签（已存在则忽略），如果 Tag 表里没有该名称则先创建。
    /// </summary>
    Task AddTagToPictureAsync(int pictureId, string tagName);

    /// <summary>
    /// 从图片移除指定标签关联（不删除 Tag 记录本身）。
    /// </summary>
    Task RemoveTagFromPictureAsync(int pictureId, string tagName);

    Task DeleteTagAsync(string tagName);

    /// <summary>
    /// 获取单个图片（含标签），用于详情弹窗。
    /// </summary>
    Task<Picture?> GetPictureByIdAsync(int pictureId);

    /// <summary>
    /// 删除图片记录及其所有标签关联（PictureTag），不删除 Tag 记录本身。
    /// </summary>
    Task DeletePictureAsync(int pictureId);
}
