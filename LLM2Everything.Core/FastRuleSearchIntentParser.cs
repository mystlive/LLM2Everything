using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LLM2Everything.Core;

public sealed class FastRuleSearchIntentParser : ISearchIntentParser
{
    private readonly EverythingQueryBuilder _queryBuilder;
    private readonly SearchIntentValidator _validator;
    private readonly JstDateResolver _dates = new();

    public FastRuleSearchIntentParser(EverythingQueryBuilder queryBuilder, SearchIntentValidator validator)
    {
        _queryBuilder = queryBuilder;
        _validator = validator;
    }

    public Task<SearchParseResult> ParseAsync(SearchInput input, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var text = Normalize(input.Text);
        var intent = new SearchIntent { Confidence = 0.7, UserReason = "高速ルールで解釈しました。" };
        var meaningful = false;

        foreach (var folder in input.SelectedFolders.Where(Directory.Exists))
        {
            intent.TargetFolders.Add(folder);
            meaningful = true;
        }

        if (input.FileTypeMode == FileTypeMode.FileType)
        {
            intent.FileTypes.AddRange(input.SelectedFileTypes);
            meaningful |= input.SelectedFileTypes.Count > 0;
        }
        if (input.FileTypeMode == FileTypeMode.Extension)
        {
            intent.Extensions.AddRange(input.SelectedExtensions.Select(DefaultFileTypes.NormalizeExtension).Where(e => e.Length > 0));
            meaningful |= intent.Extensions.Count > 0;
        }

        foreach (var type in input.FileTypeDefinitions)
        {
            if (text.Contains(type.Name, StringComparison.OrdinalIgnoreCase))
            {
                intent.FileTypes.Add(type.Name);
                meaningful = true;
            }
        }

        foreach (Match match in Regex.Matches(text, @"(?:^|[\s　])\.?([a-zA-Z0-9][a-zA-Z0-9_-]{1,8})(?:$|[\s　]|ファイル|のみ|以上|以下)"))
        {
            var ext = DefaultFileTypes.NormalizeExtension(match.Groups[1].Value);
            if (IsLikelyExtension(ext, input.FileTypeDefinitions))
            {
                intent.Extensions.Add(ext);
                meaningful = true;
            }
        }
        foreach (var knownExt in input.FileTypeDefinitions.SelectMany(f => f.Extensions).Select(DefaultFileTypes.NormalizeExtension).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (knownExt.Length > 0 && text.Contains(knownExt, StringComparison.OrdinalIgnoreCase))
            {
                intent.Extensions.Add(knownExt);
                meaningful = true;
            }
        }

        var drive = Regex.Match(text, @"([A-Za-z])\s*ドライブ");
        if (drive.Success)
        {
            intent.TargetFolders.Add(drive.Groups[1].Value.ToUpperInvariant() + @":\");
            meaningful = true;
        }

        if (text.Contains("昨日"))
        {
            intent.Modified = _dates.Yesterday(input.Now);
            intent.ContainsRelativeDate = true;
            meaningful = true;
        }
        else if (text.Contains("今日"))
        {
            intent.Modified = _dates.Today(input.Now);
            intent.ContainsRelativeDate = true;
            meaningful = true;
        }
        else if (text.Contains("先週"))
        {
            intent.Modified = _dates.LastWeek(input.Now);
            intent.ContainsRelativeDate = true;
            meaningful = true;
        }
        else if (text.Contains("今週"))
        {
            intent.Modified = _dates.ThisWeek(input.Now);
            intent.ContainsRelativeDate = true;
            meaningful = true;
        }

        var pastDays = Regex.Match(text, @"過去\s*(\d+)\s*日");
        if (pastDays.Success && int.TryParse(pastDays.Groups[1].Value, out var days))
        {
            intent.Modified = _dates.PastDays(input.Now, days);
            intent.ContainsRelativeDate = true;
            meaningful = true;
        }
        var pastHours = Regex.Match(text, @"過去\s*(\d+)\s*時間|過去\s*24\s*時間");
        if (pastHours.Success)
        {
            var hours = pastHours.Groups[1].Success && int.TryParse(pastHours.Groups[1].Value, out var h) ? h : 24;
            intent.Modified = _dates.PastHours(input.Now, hours);
            intent.ContainsRelativeDate = true;
            meaningful = true;
        }

        ParseSize(text, intent, ref meaningful);
        ParseTerms(text, intent, ref meaningful);

        if (text.Contains("ファイルのみ"))
        {
            intent.EntryKind = EntryKind.FileOnly;
            meaningful = true;
        }
        if (text.Contains("フォルダーのみ") || text.Contains("フォルダのみ"))
        {
            if (intent.EntryKind == EntryKind.FileOnly) intent.Conflicts.Add("ファイルのみとフォルダーのみが同時に指定されています。");
            intent.EntryKind = EntryKind.FolderOnly;
            meaningful = true;
        }

        intent.Extensions = intent.Extensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        intent.FileTypes = intent.FileTypes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        intent.TargetFolders = intent.TargetFolders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (!meaningful)
        {
            intent.Decision = SearchDecision.Ambiguous;
            intent.UserReason = "高速ルールだけでは検索条件を確定できません。";
        }

        _validator.Validate(intent);
        var query = _queryBuilder.Build(intent, input.FileTypeDefinitions);
        sw.Stop();
        return Task.FromResult(new SearchParseResult
        {
            Decision = intent.Decision,
            Method = ParseMethod.FastRule,
            Intent = intent,
            EverythingQuery = query,
            Elapsed = sw.Elapsed
        });
    }

    private static void ParseTerms(string text, SearchIntent intent, ref bool meaningful)
    {
        foreach (Match match in Regex.Matches(text, @"(.+?)を?除く"))
        {
            var term = CleanupTerm(match.Groups[1].Value);
            if (term.Length > 0)
            {
                intent.ExcludeTerms.Add(term);
                meaningful = true;
            }
        }
        foreach (Match match in Regex.Matches(text, @"(.+?)を?含む"))
        {
            var term = CleanupTerm(match.Groups[1].Value);
            if (term.Length > 0)
            {
                intent.IncludeTerms.Add(term);
                meaningful = true;
            }
        }
    }

    private static void ParseSize(string text, SearchIntent intent, ref bool meaningful)
    {
        foreach (Match match in Regex.Matches(text, @"(\d+(?:\.\d+)?)\s*(KB|MB|GB|TB|B)\s*(以上|以下|未満)?", RegexOptions.IgnoreCase))
        {
            var bytes = ToBytes(double.Parse(match.Groups[1].Value), match.Groups[2].Value);
            var op = match.Groups[3].Value;
            if (op is "以下" or "未満") intent.Size = intent.Size with { MaxBytes = bytes };
            else intent.Size = intent.Size with { MinBytes = bytes };
            meaningful = true;
        }
    }

    private static long ToBytes(double value, string unit) => unit.ToUpperInvariant() switch
    {
        "TB" => (long)(value * 1024L * 1024L * 1024L * 1024L),
        "GB" => (long)(value * 1024L * 1024L * 1024L),
        "MB" => (long)(value * 1024L * 1024L),
        "KB" => (long)(value * 1024L),
        _ => (long)value
    };

    private static bool IsLikelyExtension(string ext, IReadOnlyList<FileTypeDefinition> fileTypes)
    {
        if (ext.Length is < 2 or > 10) return false;
        var known = fileTypes.SelectMany(f => f.Extensions).Select(DefaultFileTypes.NormalizeExtension);
        return known.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private static string CleanupTerm(string value) =>
        value.Replace("ファイル名に", "").Replace("名前に", "").Trim(' ', '　', '、', '。');

    private static string Normalize(string value) => value.Trim().Replace('　', ' ');
}
