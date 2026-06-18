namespace LLM2Everything.Core;

public static class ProductInfo
{
    public const string ProductName = "LLM2Everything";
    public const string AppDisplayName = "LLM2Everything";
    public const string WindowTitle = "LLM2Everything - 日本語ファイル検索";
    public const string LogAppName = "LLM2Everything";
    public const string PromptVersion = "search-intent-v1";

    public static string LocalAppDataFolder
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("LLM2EVERYTHING_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(overrideRoot))
                return overrideRoot;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName);
        }
    }
}
