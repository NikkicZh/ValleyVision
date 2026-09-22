namespace ValleyVision;

internal sealed record ExternalTools(string YtDlp, string Ffmpeg, string Ffplay)
{
    internal static bool TryResolve(ModConfig config, out ExternalTools? tools, out string error)
    {
        tools = null;
        if (!TryFind(config.YtDlpPath, "yt-dlp.exe", out string ytDlp))
        {
            error = "找不到 yt-dlp；请在 config.json 配置 YtDlpPath，或将它加入 PATH。";
            return false;
        }

        if (!TryFind(config.FfmpegPath, "ffmpeg.exe", out string ffmpeg))
        {
            error = "找不到 FFmpeg；请在 config.json 配置 FfmpegPath，或将它加入 PATH。";
            return false;
        }

        if (!TryFind(config.FfplayPath, "ffplay.exe", out string ffplay))
        {
            error = "找不到 ffplay；请在 config.json 配置 FfplayPath，或将它加入 PATH。";
            return false;
        }

        tools = new ExternalTools(ytDlp, ffmpeg, ffplay);
        error = string.Empty;
        return true;
    }

    private static bool TryFind(string? configuredPath, string name, out string path)
    {
        path = string.Empty;
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (Path.IsPathFullyQualified(configuredPath) && File.Exists(configuredPath))
            {
                path = Path.GetFullPath(configuredPath);
                return true;
            }

            return false;
        }

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(directory.Trim('"'), name);
                if (File.Exists(candidate))
                {
                    path = Path.GetFullPath(candidate);
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Ignore malformed PATH entries.
            }
        }

        return false;
    }
}
