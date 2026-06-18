using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LLM2Everything.Core;

namespace LLM2Everything.Infrastructure;

public sealed class EverythingInstallationDetector : IEverythingInstallationDetector
{
    public string? FindEsExe() => FindInCandidates("es.exe", [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "es.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "es.exe")
    ]);

    public string? FindEverythingExe() => FindInCandidates("Everything.exe", [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "Everything.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "Everything.exe")
    ]);

    private static string? FindInCandidates(string exe, IEnumerable<string> candidates)
    {
        foreach (var path in candidates)
            if (File.Exists(path)) return path;
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}

public sealed class EverythingProcessService : IEverythingProcessService
{
    public bool IsEverythingRunning() =>
        Process.GetProcessesByName("Everything").Any();

    public async Task<bool> StartEverythingAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        for (var i = 0; i < 30; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsEverythingRunning()) return true;
            await Task.Delay(500, cancellationToken);
        }
        return IsEverythingRunning();
    }
}

public sealed class EsExeSearchService : IEverythingSearchService
{
    private readonly Func<string> _pathProvider;

    public EsExeSearchService(Func<string> pathProvider) => _pathProvider = pathProvider;

    public async Task<EsSearchResponse> SearchAsync(EsSearchRequest request, CancellationToken cancellationToken)
    {
        var exe = _pathProvider();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            throw new FileNotFoundException("es.exe が見つかりません。設定画面でパスを指定してください。", exe);

        ValidateQuery(request.Query);
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(request.Timeout);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(exe)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (request.Limit is > 0)
        {
            process.StartInfo.ArgumentList.Add("-n");
            process.StartInfo.ArgumentList.Add(request.Limit.Value.ToString());
        }
        if (request.SortDateModifiedDescending)
            process.StartInfo.ArgumentList.Add("-sort-date-modified-descending");
        process.StartInfo.ArgumentList.Add("-dm");
        process.StartInfo.ArgumentList.Add("-size");
        process.StartInfo.ArgumentList.Add("-date-format");
        process.StartInfo.ArgumentList.Add("1");
        process.StartInfo.ArgumentList.Add(request.Query);

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);
        sw.Stop();

        var lines = (await stdoutTask).Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var results = lines.Select(ParseLine).Where(r => r is not null).Cast<SearchResultItem>().ToList();
        return new EsSearchResponse
        {
            Results = results,
            ExitCode = process.ExitCode,
            StandardError = await stderrTask,
            Elapsed = sw.Elapsed,
            LimitReached = request.Limit is not null && results.Count >= request.Limit
        };
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        var exe = _pathProvider();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return "";
        using var process = Process.Start(new ProcessStartInfo(exe)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "-version" }
        });
        if (process is null) return "";
        var text = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return text.Trim();
    }

    public async Task<bool> TestAsync(CancellationToken cancellationToken)
    {
        var response = await SearchAsync(new EsSearchRequest { Query = ".", Limit = 1, Timeout = TimeSpan.FromSeconds(5) }, cancellationToken);
        return response.ExitCode == 0;
    }

    private static SearchResultItem? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var match = Regex.Match(line, @"^(?<date>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})\s+(?:(?<size>[\d,]+)\s+)?(?<path>[A-Za-z]:\\.+|\\\\.+)$");
        if (match.Success)
        {
            var path = match.Groups["path"].Value;
            long? size = long.TryParse(match.Groups["size"].Value.Replace(",", ""), out var parsedSize) ? parsedSize : null;
            DateTimeOffset? modified = DateTime.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate)
                ? new DateTimeOffset(parsedDate)
                : null;
            return new SearchResultItem
            {
                FullPath = path,
                FileName = Path.GetFileName(path),
                Extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
                SizeBytes = size ?? (File.Exists(path) ? new FileInfo(path).Length : null),
                ModifiedAt = modified ?? ((File.Exists(path) || Directory.Exists(path)) ? File.GetLastWriteTime(path) : null),
                IsFolder = Directory.Exists(path)
            };
        }

        var ext = Path.GetExtension(line).TrimStart('.').ToLowerInvariant();
        return new SearchResultItem
        {
            FullPath = line,
            FileName = Path.GetFileName(line),
            Extension = ext,
            SizeBytes = File.Exists(line) ? new FileInfo(line).Length : null,
            ModifiedAt = File.Exists(line) || Directory.Exists(line) ? File.GetLastWriteTime(line) : null,
            IsFolder = Directory.Exists(line)
        };
    }

    private static void ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("検索式が空です。");
        if (query.Any(char.IsControl)) throw new ArgumentException("検索式に制御文字が含まれています。");
        if (query.Contains('\r') || query.Contains('\n')) throw new ArgumentException("検索式に改行が含まれています。");
    }
}
