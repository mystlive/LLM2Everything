namespace LLM2Everything.Core;

public sealed class SearchIntentValidator
{
    public SearchIntent Validate(SearchIntent intent)
    {
        intent.Conflicts.Clear();
        if (intent.Modified.Start is not null && intent.Modified.End is not null && intent.Modified.Start > intent.Modified.End)
            intent.Conflicts.Add("更新日時の開始が終了より後です。");
        if (intent.Created.Start is not null && intent.Created.End is not null && intent.Created.Start > intent.Created.End)
            intent.Conflicts.Add("作成日時の開始が終了より後です。");
        if (intent.Size.MinBytes is not null && intent.Size.MaxBytes is not null && intent.Size.MinBytes > intent.Size.MaxBytes)
            intent.Conflicts.Add("最小サイズが最大サイズより大きいです。");
        if (intent.EntryKind != EntryKind.Any && intent.Conflicts.Any(c => c.Contains("ファイルのみ") && c.Contains("フォルダーのみ")))
            intent.Conflicts.Add("ファイルのみとフォルダーのみが同時に指定されています。");

        foreach (var ext in intent.Extensions)
        {
            if (ext.Length == 0 || ext.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '-')))
                intent.Conflicts.Add($"拡張子 '{ext}' が不正です。");
        }

        if (intent.Conflicts.Count > 0)
        {
            intent.Decision = SearchDecision.NotSearchable;
            intent.UserReason = string.Join(Environment.NewLine, intent.Conflicts);
        }
        return intent;
    }
}
