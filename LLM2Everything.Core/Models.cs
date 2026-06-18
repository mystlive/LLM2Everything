using System.Text.Json.Serialization;

namespace LLM2Everything.Core;

public enum SearchDecision { Searchable, Ambiguous, NotSearchable }
public enum ParseMethod { FastRule, Llm, Cache, Manual }
public enum EntryKind { Any, FileOnly, FolderOnly }
public enum FileTypeMode { None, FileType, Extension }

public sealed record DateRange(DateTimeOffset? Start, DateTimeOffset? End, string? Label = null);
public sealed record SizeRange(long? MinBytes, long? MaxBytes);

public sealed class FileTypeDefinition
{
    public string Name { get; set; } = "";
    public List<string> Extensions { get; set; } = [];
}

public sealed class SearchIntent
{
    public SearchDecision Decision { get; set; } = SearchDecision.Searchable;
    public List<string> IncludeTerms { get; set; } = [];
    public List<string> ExcludeTerms { get; set; } = [];
    public List<string> TargetFolders { get; set; } = [];
    public List<string> FileTypes { get; set; } = [];
    public List<string> Extensions { get; set; } = [];
    public EntryKind EntryKind { get; set; } = EntryKind.Any;
    public SizeRange Size { get; set; } = new(null, null);
    public DateRange Modified { get; set; } = new(null, null);
    public DateRange Created { get; set; } = new(null, null);
    public List<AmbiguousCandidate> AmbiguousCandidates { get; set; } = [];
    public List<string> Conflicts { get; set; } = [];
    public string UserReason { get; set; } = "";
    public double Confidence { get; set; }
    public bool ContainsRelativeDate { get; set; }
}

public sealed class AmbiguousCandidate
{
    public string SummaryJa { get; set; } = "";
    public SearchIntent Intent { get; set; } = new();
}

public sealed class SearchParseResult
{
    public SearchDecision Decision { get; set; }
    public ParseMethod Method { get; set; }
    public SearchIntent Intent { get; set; } = new();
    public string EverythingQuery { get; set; } = "";
    public string ModelName { get; set; } = "";
    public TimeSpan Elapsed { get; set; }
}

public sealed class AppSettings
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "";
    public int OllamaTimeoutSeconds { get; set; } = 60;
    public string EsExePath { get; set; } = "";
    public string EverythingPath { get; set; } = "";
    public int EverythingTimeoutSeconds { get; set; } = 30;
    public int? ResultLimit { get; set; } = 1000;
    public string TimeZoneId { get; set; } = "Asia/Tokyo";
    public bool DiagnosticMode { get; set; }
    public string? SortColumn { get; set; }
    public bool SortDescending { get; set; }
    public List<string> RegisteredFolders { get; set; } = [];
}

public sealed class SearchResultItem
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Extension { get; set; } = "";
    public long? SizeBytes { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public bool IsFolder { get; set; }
}

public sealed class HistoryEntry
{
    public string Input { get; set; } = "";
    public List<string> SelectedFolders { get; set; } = [];
    public List<string> FileTypes { get; set; } = [];
    public List<string> Extensions { get; set; } = [];
    public string EverythingQuery { get; set; } = "";
    public DateTimeOffset ExecutedAt { get; set; }
    public int ResultCount { get; set; }
    public ParseMethod Method { get; set; }
}

public sealed class CacheEntry
{
    public string Key { get; set; } = "";
    public SearchParseResult Result { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public string? JstDateKey { get; set; }
}

public sealed class OperationState
{
    public bool IsBusy { get; set; }
    public string StatusText { get; set; } = "待機中";
    public TimeSpan Elapsed { get; set; }
    public TimeSpan? Remaining { get; set; }
}

public sealed class EsSearchRequest
{
    public string Query { get; set; } = "";
    public int? Limit { get; set; } = 1000;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool SortDateModifiedDescending { get; set; }
}

public sealed class EsSearchResponse
{
    public List<SearchResultItem> Results { get; set; } = [];
    public int ExitCode { get; set; }
    public string StandardError { get; set; } = "";
    public TimeSpan Elapsed { get; set; }
    public bool LimitReached { get; set; }
}
