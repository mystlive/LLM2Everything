using LLM2Everything.Core;
using LLM2Everything.Infrastructure;

var tests = new List<(string Name, Func<Task> Body)>
{
    ("高速ルール: PDF", async () => await ParseAssert("PDF", r => r.Intent.Extensions.Contains("pdf"))),
    ("高速ルール: 昨日の画像", async () => await ParseAssert("昨日の画像", r => r.Intent.FileTypes.Contains("画像") && r.Intent.Modified.Label == "昨日")),
    ("高速ルール: 昨日のエクセル", async () => await ParseAssert("昨日のエクセル", r => r.Intent.Extensions.Contains("xlsx") && r.Intent.Modified.Label == "昨日")),
    ("高速ルール: 17日のxlsx", async () => await ParseAssert("17日のxlsx", r => r.Intent.Extensions.Contains("xlsx") && r.Intent.Modified.Start!.Value.Day == 17)),
    ("高速ルール: EドライブのGGUF", async () => await ParseAssert("EドライブのGGUF", r => r.Intent.TargetFolders.Contains(@"E:\") && r.Intent.Extensions.Contains("gguf"))),
    ("高速ルール: 1GB以上のZIP", async () => await ParseAssert("1GB以上のZIP", r => r.Intent.Size.MinBytes == 1073741824L && r.Intent.Extensions.Contains("zip"))),
    ("高速ルール: 除外語", async () => await ParseAssert("releaseを除くソースコード", r => r.Intent.ExcludeTerms.Contains("release") && r.Intent.FileTypes.Contains("ソースコード"))),
    ("日時: JST今日", () => DateAssert(d => d.Today(new DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero)).Start!.Value.Day == 2)),
    ("日時: JST昨日", () => DateAssert(d => d.Yesterday(new DateTimeOffset(2026, 1, 1, 15, 0, 0, TimeSpan.Zero)).Start!.Value.Day == 1)),
    ("日時: 先週", () => DateAssert(d => d.LastWeek(new DateTimeOffset(2026, 6, 18, 1, 0, 0, TimeSpan.Zero)).Start!.Value.DayOfWeek == DayOfWeek.Monday)),
    ("日時: 過去24時間と昨日の違い", () => DateAssert(d => d.PastHours(new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.FromHours(9)), 24).Start != d.Yesterday(new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.FromHours(9))).Start)),
    ("矛盾: 日時逆転", () => ValidatorAssert(i => i.Modified = new DateRange(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(-1)))),
    ("矛盾: サイズ逆転", () => ValidatorAssert(i => i.Size = new SizeRange(100, 1))),
    ("検索式: 日本語パスと空白", () => QueryAssert(i => i.TargetFolders.Add(@"D:\日本語 パス"), q => q.Contains("path:\"D:\\日本語 パス\""))),
    ("検索式: 引用符と除外", () => QueryAssert(i => i.ExcludeTerms.Add("bad name"), q => q.Contains("!\"bad name\""))),
    ("検索式: 拡張子複数", () => QueryAssert(i => { i.Extensions.Add("pdf"); i.Extensions.Add("docx"); }, q => q.Contains("<ext:docx|ext:pdf>") || q.Contains("<ext:pdf|ext:docx>"))),
    ("保存: 履歴20件上限", HistoryLimitAssert),
    ("保存: キャッシュ100件上限", CacheLimitAssert),
    ("保存: JSON破損バックアップ復旧", BackupAssert)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Console.WriteLine($"{failed} tests failed.");
    return 1;
}
Console.WriteLine($"{tests.Count} tests passed.");
return 0;

static async Task ParseAssert(string text, Func<SearchParseResult, bool> assertion)
{
    var parser = new FastRuleSearchIntentParser(new EverythingQueryBuilder(), new SearchIntentValidator());
    var result = await parser.ParseAsync(new SearchInput
    {
        Text = text,
        FileTypeDefinitions = DefaultFileTypes.Create(),
        Now = new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.FromHours(9))
    }, CancellationToken.None);
    if (!assertion(result))
        throw new InvalidOperationException($"unexpected parse: {System.Text.Json.JsonSerializer.Serialize(result)}");
}

static Task DateAssert(Func<JstDateResolver, bool> assertion)
{
    if (!assertion(new JstDateResolver())) throw new InvalidOperationException("date assertion failed");
    return Task.CompletedTask;
}

static Task ValidatorAssert(Action<SearchIntent> arrange)
{
    var intent = new SearchIntent();
    arrange(intent);
    new SearchIntentValidator().Validate(intent);
    if (intent.Decision != SearchDecision.NotSearchable) throw new InvalidOperationException("conflict was not detected");
    return Task.CompletedTask;
}

static Task QueryAssert(Action<SearchIntent> arrange, Func<string, bool> assertion)
{
    var intent = new SearchIntent();
    arrange(intent);
    var query = new EverythingQueryBuilder().Build(intent, DefaultFileTypes.Create());
    if (!assertion(query)) throw new InvalidOperationException(query);
    return Task.CompletedTask;
}

static async Task HistoryLimitAssert()
{
    var temp = NewTempRoot();
    Environment.SetEnvironmentVariable("LLM2EVERYTHING_DATA_DIR", temp);
    var repo = new HistoryRepository(new JsonAtomicFileStore());
    await repo.SaveAsync(Enumerable.Range(0, 25).Select(i => new HistoryEntry { Input = i.ToString() }).ToList());
    var loaded = await repo.LoadAsync();
    if (loaded.Count != 20) throw new InvalidOperationException($"count={loaded.Count}");
}

static async Task CacheLimitAssert()
{
    var temp = NewTempRoot();
    Environment.SetEnvironmentVariable("LLM2EVERYTHING_DATA_DIR", temp);
    var repo = new CacheRepository(new JsonAtomicFileStore());
    await repo.SaveAsync(Enumerable.Range(0, 120).Select(i => new CacheEntry { Key = i.ToString() }).ToList());
    var loaded = await repo.LoadAsync();
    if (loaded.Count != 100) throw new InvalidOperationException($"count={loaded.Count}");
}

static async Task BackupAssert()
{
    var temp = NewTempRoot();
    var store = new JsonAtomicFileStore();
    var path = Path.Combine(temp, "settings.json");
    await store.SaveAsync(path, new AppSettings { OllamaModel = "ok" });
    await store.SaveAsync(path, new AppSettings { OllamaModel = "newer" });
    await File.WriteAllTextAsync(path, "{ broken");
    var loaded = await store.LoadAsync<AppSettings>(path);
    if (loaded?.OllamaModel != "ok") throw new InvalidOperationException("backup was not restored");
}

static string NewTempRoot()
{
    var root = Path.Combine(Path.GetTempPath(), "LLM2EverythingTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    return root;
}
