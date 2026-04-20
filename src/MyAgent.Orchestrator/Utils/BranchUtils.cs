using System.Text.RegularExpressions;

namespace MyAgent.Orchestrator.Utils;

public static class BranchUtils
{
    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "untitled";

        var slug = text.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9]+", "-");
        slug = slug.Trim('-');
        if (slug.Length > 50) slug = slug[..50].TrimEnd('-');
        if (string.IsNullOrEmpty(slug)) slug = "untitled";
        return slug;
    }

    public static string MakeBranchName(int workItemId, string title)
        => $"feature/ae/{workItemId}-{Slugify(title)}";
}
