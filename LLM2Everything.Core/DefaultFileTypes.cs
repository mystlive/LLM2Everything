namespace LLM2Everything.Core;

public static class DefaultFileTypes
{
    public static List<FileTypeDefinition> Create() =>
    [
        new() { Name = "文書", Extensions = ["pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "md"] },
        new() { Name = "画像", Extensions = ["jpg", "jpeg", "png", "gif", "bmp", "webp", "tif", "tiff", "psd"] },
        new() { Name = "動画", Extensions = ["mp4", "mov", "mkv", "avi", "wmv", "webm"] },
        new() { Name = "音声", Extensions = ["mp3", "wav", "flac", "aac", "m4a", "ogg"] },
        new() { Name = "圧縮ファイル", Extensions = ["zip", "7z", "rar", "tar", "gz", "bz2", "xz"] },
        new() { Name = "ソースコード", Extensions = ["cs", "js", "ts", "py", "java", "cpp", "c", "h", "hpp", "html", "css", "json", "xml", "yml", "yaml"] },
        new() { Name = "AIモデル", Extensions = ["gguf", "safetensors", "onnx", "pt", "pth", "ckpt"] },
        new() { Name = "3Dデータ", Extensions = ["obj", "fbx", "stl", "blend", "glb", "gltf", "3mf"] }
    ];

    public static string NormalizeExtension(string value)
    {
        var normalized = value.Trim().TrimStart('.').ToLowerInvariant();
        return new string(normalized.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
    }
}
