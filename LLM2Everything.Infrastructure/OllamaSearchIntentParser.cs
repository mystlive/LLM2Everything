using System.Diagnostics;
using System.Text.Json;
using LLM2Everything.Core;

namespace LLM2Everything.Infrastructure;

public sealed class OllamaSearchIntentParser : ISearchIntentParser
{
    private readonly IOllamaClient _client;
    private readonly EverythingQueryBuilder _builder;
    private readonly SearchIntentValidator _validator;
    private readonly AppSettings _settings;

    public OllamaSearchIntentParser(IOllamaClient client, EverythingQueryBuilder builder, SearchIntentValidator validator, AppSettings settings)
    {
        _client = client;
        _builder = builder;
        _validator = validator;
        _settings = settings;
    }

    public async Task<SearchParseResult> ParseAsync(SearchInput input, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(_settings.OllamaModel))
            return NotSearchable("Ollamaモデルが設定されていません。");

        var prompt = BuildPrompt(input);
        var raw = await _client.GenerateAsync(new OllamaGenerateRequest
        {
            Url = _settings.OllamaUrl,
            Model = _settings.OllamaModel,
            Prompt = prompt,
            TimeoutSeconds = _settings.OllamaTimeoutSeconds
        }, cancellationToken);

        var dto = JsonSerializer.Deserialize<LlmIntentDto>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? throw new JsonException("LLM応答JSONを解釈できませんでした。");
        var intent = new SearchIntent
        {
            Decision = Enum.TryParse<SearchDecision>(dto.Decision, true, out var d) ? d : SearchDecision.NotSearchable,
            IncludeTerms = dto.IncludeTerms ?? [],
            ExcludeTerms = dto.ExcludeTerms ?? [],
            TargetFolders = dto.TargetFolders ?? [],
            FileTypes = dto.FileTypes ?? [],
            Extensions = (dto.Extensions ?? []).Select(DefaultFileTypes.NormalizeExtension).Where(e => e.Length > 0).ToList(),
            EntryKind = Enum.TryParse<EntryKind>(dto.EntryKind, true, out var k) ? k : EntryKind.Any,
            Size = new(dto.MinSizeBytes, dto.MaxSizeBytes),
            UserReason = dto.UserReason ?? "",
            Confidence = dto.Confidence
        };
        _validator.Validate(intent);
        sw.Stop();
        return new SearchParseResult
        {
            Decision = intent.Decision,
            Method = ParseMethod.Llm,
            Intent = intent,
            EverythingQuery = _builder.Build(intent, input.FileTypeDefinitions),
            ModelName = _settings.OllamaModel,
            Elapsed = sw.Elapsed
        };
    }

    private static SearchParseResult NotSearchable(string reason)
    {
        var intent = new SearchIntent { Decision = SearchDecision.NotSearchable, UserReason = reason };
        return new SearchParseResult { Decision = SearchDecision.NotSearchable, Method = ParseMethod.Llm, Intent = intent };
    }

    private static string BuildPrompt(SearchInput input) =>
        $"""
        あなたはWindowsファイル検索条件だけを構造化する変換器です。Everything検索式は生成しません。
        現在日時: {input.Now:O}
        タイムゾーン: {input.TimeZoneId}
        現在日: {input.Now.ToOffset(TimeSpan.FromHours(9)):yyyy-MM-dd}
        曜日: {input.Now.ToOffset(TimeSpan.FromHours(9)):dddd}
        利用可能ファイルタイプ: {string.Join(", ", input.FileTypeDefinitions.Select(f => f.Name))}
        ユーザー入力: {input.Text}
        ファイル名、種類、場所、日時、サイズとして解釈できる条件をJSONだけで返してください。
        """;

    private sealed class LlmIntentDto
    {
        public string Decision { get; set; } = "NotSearchable";
        public List<string>? IncludeTerms { get; set; }
        public List<string>? ExcludeTerms { get; set; }
        public List<string>? TargetFolders { get; set; }
        public List<string>? FileTypes { get; set; }
        public List<string>? Extensions { get; set; }
        public string EntryKind { get; set; } = "Any";
        public long? MinSizeBytes { get; set; }
        public long? MaxSizeBytes { get; set; }
        public string? UserReason { get; set; }
        public double Confidence { get; set; }
    }
}
