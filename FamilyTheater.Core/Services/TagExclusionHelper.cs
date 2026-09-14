using FamilyTheater.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyTheater.Core.Services;

internal static class TagExclusionHelper
{
    public static string Normalize(string tagName)
    {
        return tagName.Trim().ToUpperInvariant();
    }

    public static async Task<HashSet<string>> GetExcludedTagNamesAsync(AppDbContext db, int category)
    {
        var names = await db.ExcludedTags
            .Where(tag => tag.Category == category)
            .Select(tag => tag.NormalizedTagName)
            .ToListAsync();

        return names.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsExcluded(string tagName, IReadOnlySet<string> excludedTagNames)
    {
        return excludedTagNames.Contains(Normalize(tagName));
    }

    public static List<string> FilterExcludedTags(IEnumerable<string> tagNames, IReadOnlySet<string> excludedTagNames)
    {
        return tagNames
            .Where(tagName => !IsExcluded(tagName, excludedTagNames))
            .ToList();
    }

    public static async Task<List<string>> GetSavedTagNamesAsync(AppDbContext db, int category)
    {
        return await db.SavedTags
            .Where(tag => tag.Category == category)
            .Select(tag => tag.TagName)
            .AsNoTracking()
            .ToListAsync();
    }

    public static List<string> MergeVisibleTags(
        IEnumerable<string> tagNames,
        IEnumerable<string> savedTagNames,
        IReadOnlySet<string> excludedTagNames)
    {
        return tagNames
            .Concat(savedTagNames)
            .Where(name => !IsExcluded(name, excludedTagNames))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();
    }

    public static async Task AddSavedTagAsync(AppDbContext db, int category, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var normalizedName = Normalize(name);
        var exists = await db.SavedTags.AnyAsync(tag =>
            tag.Category == category &&
            tag.NormalizedTagName == normalizedName);
        if (exists)
        {
            return;
        }

        db.SavedTags.Add(new SavedTag
        {
            Category = category,
            TagName = name,
            NormalizedTagName = normalizedName
        });
    }

    public static async Task RemoveSavedTagAsync(AppDbContext db, int category, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var normalizedName = Normalize(name);
        var savedTags = await db.SavedTags
            .Where(tag => tag.Category == category && tag.NormalizedTagName == normalizedName)
            .ToListAsync();

        db.SavedTags.RemoveRange(savedTags);
    }

    public static async Task AddExcludedTagAsync(AppDbContext db, int category, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var normalizedName = Normalize(name);
        var exists = await db.ExcludedTags.AnyAsync(tag =>
            tag.Category == category &&
            tag.NormalizedTagName == normalizedName);
        if (exists)
        {
            return;
        }

        db.ExcludedTags.Add(new ExcludedTag
        {
            Category = category,
            TagName = name,
            NormalizedTagName = normalizedName
        });
    }

    public static async Task RemoveExcludedTagAsync(AppDbContext db, int category, string tagName)
    {
        var name = tagName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var normalizedName = Normalize(name);
        var excludedTags = await db.ExcludedTags
            .Where(tag => tag.Category == category && tag.NormalizedTagName == normalizedName)
            .ToListAsync();

        db.ExcludedTags.RemoveRange(excludedTags);
    }
}
