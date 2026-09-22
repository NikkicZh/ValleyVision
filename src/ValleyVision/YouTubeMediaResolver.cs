using System.Text.Json;

namespace ValleyVision;

internal static class YouTubeMediaResolver
{
    internal static async Task<ResolvedMedia> ResolveAsync(YouTubeVideoLink link, string ytDlpPath,
        ModConfig config, CancellationToken token)
    {
        var arguments = new List<string> { "--ignore-config", "--no-playlist", "--no-warnings", "-J", "-f",
            "bv*[height<=360][vcodec^=avc1]+ba/bv*[vcodec^=avc1]+ba/b" };
        string runtime = config.YouTubeJsRuntime?.Trim().ToLowerInvariant() ?? string.Empty;
        string path = config.YouTubeJsRuntimePath?.Trim() ?? string.Empty;
        if (runtime.Length > 0 && runtime is not ("deno" or "node"))
            throw new MediaException("YouTubeJsRuntime 仅支持 deno 或 node。", MediaError.Tool);
        if (path.Length > 0 && runtime.Length == 0)
            throw new MediaException("请同时配置 YouTubeJsRuntime。", MediaError.Tool);
        if (path.Length > 0 && (!Path.IsPathFullyQualified(path) || !File.Exists(path)))
            throw new MediaException("YouTubeJsRuntimePath 无效；请指定已安装运行环境的绝对路径。", MediaError.Tool);
        if (runtime.Length > 0)
        {
            arguments.Add("--js-runtimes");
            arguments.Add(path.Length > 0 ? $"{runtime}:{path}" : runtime);
        }

        var result = await YtDlpRunner.RunAsync(ytDlpPath, arguments, link.Url, token);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Json))
        {
            string error = result.Error.ToLowerInvariant();
            if (error.Contains("ejs") || error.Contains("javascript runtime") || error.Contains("js runtime")
                || error.Contains("deno") || error.Contains("node.js"))
                throw new MediaException("YouTube 脚本环境不可用；请检查 EJS、YouTubeJsRuntime 和 YouTubeJsRuntimePath。", MediaError.Tool);
            throw new MediaException("无法解析此 YouTube 视频；请确认视频公开且无需登录。", MediaError.Unplayable);
        }
        return Parse(result.Json, link);
    }

    internal static ResolvedMedia Parse(string json, YouTubeVideoLink link)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("entries", out _)
                || Read(root, "_type") is "playlist" or "multi_video"
                || Read(root, "extractor_key") != "Youtube"
                || Read(root, "id") != link.VideoId
                || !YouTubeVideoLink.TryParse(Read(root, "webpage_url"), out YouTubeVideoLink? page)
                || page!.VideoId != link.VideoId)
                throw new MediaException("YouTube 视频地址或解析结果不受支持。", MediaError.Unsupported);

            if (Read(root, "is_live") == "true" || Read(root, "live_status") is "is_live" or "is_upcoming" or "was_live")
                throw new MediaException("暂不支持 YouTube 直播。", MediaError.Unsupported);
            if (root.TryGetProperty("is_live", out JsonElement live) && live.ValueKind == JsonValueKind.True)
                throw new MediaException("暂不支持 YouTube 直播。", MediaError.Unsupported);

            MediaStream? video = null;
            MediaStream? audio = null;
            if (root.TryGetProperty("requested_formats", out JsonElement formats) && formats.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement format in formats.EnumerateArray())
                {
                    if (video is null && Codec(format, "vcodec")) video = Stream(format, root);
                    if (audio is null && Codec(format, "acodec")) audio = Stream(format, root);
                }
            }
            else
            {
                if (Codec(root, "vcodec")) video = Stream(root, root);
                if (Codec(root, "acodec")) audio = Stream(root, root);
            }
            if (video is null || audio is null || !Http(video.Url) || !Http(audio.Url))
                throw new MediaException("该 YouTube 视频没有可用的独立画面和声音。", MediaError.Unplayable);
            return new ResolvedMedia(video, audio);
        }
        catch (JsonException)
        {
            throw new MediaException("视频解析结果无效。", MediaError.Unplayable);
        }
    }

    private static bool Codec(JsonElement element, string name) => Read(element, name) is not ("" or "none");
    private static string Read(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? string.Empty : string.Empty;
    private static MediaStream Stream(JsonElement format, JsonElement root)
    {
        string Header(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty("http_headers", out JsonElement headers) ? Read(headers, name) : string.Empty;
        string agent = Header(format, "User-Agent");
        string referer = Header(format, "Referer");
        return new MediaStream(Read(format, "url"), agent.Length > 0 ? agent : Header(root, "User-Agent"),
            referer.Length > 0 ? referer : Header(root, "Referer"));
    }
    private static bool Http(string text) => Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
        && uri.Scheme is "http" or "https";
}
