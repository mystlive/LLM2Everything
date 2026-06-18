using System.Text;

namespace LLM2Everything.Core;

public sealed class EverythingQueryBuilder
{
    public string Build(SearchIntent intent, IReadOnlyList<FileTypeDefinition> fileTypes)
    {
        if (intent.Decision != SearchDecision.Searchable)
            return "";

        var parts = new List<string>();
        foreach (var folder in intent.TargetFolders)
            parts.Add($"path:{Quote(folder)}");

        var extensions = new SortedSet<string>(intent.Extensions.Select(DefaultFileTypes.NormalizeExtension).Where(e => e.Length > 0), StringComparer.OrdinalIgnoreCase);
        foreach (var type in intent.FileTypes)
        {
            var def = fileTypes.FirstOrDefault(f => string.Equals(f.Name, type, StringComparison.OrdinalIgnoreCase));
            if (def is not null)
                foreach (var ext in def.Extensions.Select(DefaultFileTypes.NormalizeExtension).Where(e => e.Length > 0))
                    extensions.Add(ext);
        }
        if (extensions.Count == 1)
            parts.Add($"ext:{extensions.First()}");
        else if (extensions.Count > 1)
            parts.Add($"<" + string.Join("|", extensions.Select(e => $"ext:{e}")) + ">");

        foreach (var term in intent.IncludeTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
            parts.Add(QuoteToken(term));
        foreach (var term in intent.ExcludeTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
            parts.Add("!" + QuoteToken(term));

        if (intent.EntryKind == EntryKind.FileOnly) parts.Add("file:");
        if (intent.EntryKind == EntryKind.FolderOnly) parts.Add("folder:");
        if (intent.Size.MinBytes is not null) parts.Add($"size:>={intent.Size.MinBytes}");
        if (intent.Size.MaxBytes is not null) parts.Add($"size:<={intent.Size.MaxBytes}");
        AddDate(parts, "dm", intent.Modified);
        AddDate(parts, "dc", intent.Created);

        return string.Join(" ", parts);
    }

    private static void AddDate(List<string> parts, string prefix, DateRange range)
    {
        if (range.Start is not null)
            parts.Add($"{prefix}:>={range.Start.Value:yyyy-MM-dd HH:mm:ss}");
        if (range.End is not null)
            parts.Add($"{prefix}:<={range.End.Value:yyyy-MM-dd HH:mm:ss}");
    }

    private static string QuoteToken(string value)
    {
        var clean = Clean(value);
        return clean.Any(char.IsWhiteSpace) ? Quote(clean) : clean;
    }

    private static string Quote(string value) => "\"" + Clean(value).Replace("\"", "\\\"") + "\"";

    private static string Clean(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (!char.IsControl(ch) && ch != '\r' && ch != '\n')
                sb.Append(ch);
        }
        return sb.ToString();
    }
}
