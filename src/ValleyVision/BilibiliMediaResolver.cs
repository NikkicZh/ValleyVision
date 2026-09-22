using System.Text.Json;

namespace ValleyVision;

internal static class BilibiliMediaResolver
{
    internal static async Task<ResolvedMedia> ResolveAsync(
        BilibiliVideoLink link, string ytDlpPath, CancellationToken cancellationToken,
        TimeSpan? timeoutOverride = null)
    {
        var arguments = new List<string>
        {
            "--ignore-config", "--no-playlist", "--no-warnings", "-J", "-f",
            "bv*[height<=360][vcodec^=avc1]+ba[acodec^=mp4a]/b[height<=360][vcodec^=avc1]/bv*[vcodec^=avc1]+ba/b"
        };
        if (link.Page > 1)
        {
            arguments.Add("--playlist-items");
            arguments.Add(link.Page.ToString());
        }
        var result = await YtDlpRunner.RunAsync(ytDlpPath, arguments, link.Url, cancellationToken, timeoutOverride);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Json))
            throw new MediaException("无法解析此 B 站视频；请确认它公开可访问。", MediaError.Unplayable);
        return Parse(result.Json, link);
    }

    private static ResolvedMedia Parse(string json, BilibiliVideoLink link)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("entries", out JsonElement entries) && entries.ValueKind == JsonValueKind.Array)
            {
                root = entries.EnumerateArray().FirstOrDefault();
            }

            string pageUrl = ReadString(root, "webpage_url");
            if (!link.MatchesResolvedPage(pageUrl))
            {
                throw new MediaException("短链或视频地址未指向支持的 B 站 BV 视频。", MediaError.Unsupported);
            }

            string video = string.Empty;
            string audio = string.Empty;
            string userAgent = ReadHeader(root, "User-Agent");
            string referer = ReadHeader(root, "Referer");
            string videoAgent = string.Empty;
            string audioAgent = string.Empty;
            string videoReferer = string.Empty;
            string audioReferer = string.Empty;
            if (root.TryGetProperty("requested_formats", out JsonElement formats)
                && formats.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement format in formats.EnumerateArray())
                {
                    string url = ReadString(format, "url");
                    if (ReadString(format, "vcodec") != "none" && video.Length == 0)
                    {
                        video = url;
                        videoAgent = ReadHeader(format, "User-Agent");
                        videoReferer = ReadHeader(format, "Referer");
                    }

                    if (ReadString(format, "acodec") != "none" && audio.Length == 0)
                    {
                        audio = url;
                        audioAgent = ReadHeader(format, "User-Agent");
                        audioReferer = ReadHeader(format, "Referer");
                    }

                    userAgent = userAgent.Length == 0 ? ReadHeader(format, "User-Agent") : userAgent;
                    referer = referer.Length == 0 ? ReadHeader(format, "Referer") : referer;
                }
            }

            video = video.Length == 0 ? ReadString(root, "url") : video;
            audio = audio.Length == 0 && ReadString(root, "acodec") != "none" ? video : audio;
            if (!IsHttpMediaUrl(video) || !IsHttpMediaUrl(audio))
            {
                throw new MediaException("该视频没有可用的画面或声音。", MediaError.Unplayable);
            }

            string fallbackReferer = referer.Length == 0 ? "https://www.bilibili.com/" : referer;
            return new ResolvedMedia(
                new MediaStream(video, videoAgent.Length > 0 ? videoAgent : userAgent,
                    videoReferer.Length > 0 ? videoReferer : fallbackReferer),
                new MediaStream(audio, audioAgent.Length > 0 ? audioAgent : userAgent,
                    audioReferer.Length > 0 ? audioReferer : fallbackReferer));
        }
        catch (JsonException)
        {
            throw new MediaException("视频解析结果无效。", MediaError.Unplayable);
        }
    }

    private static string ReadString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static string ReadHeader(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("http_headers", out JsonElement headers)
            ? ReadString(headers, name) : string.Empty;
    }

    private static bool IsHttpMediaUrl(string text)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https";
    }
}

internal enum MediaError { Tool, Timeout, Unsupported, Unplayable }

internal sealed class MediaException : Exception
{
    internal MediaException(string message, MediaError reason) : base(message)
    {
        this.Reason = reason;
    }

    internal MediaError Reason { get; }
}
