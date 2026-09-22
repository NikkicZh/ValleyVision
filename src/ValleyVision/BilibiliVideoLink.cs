using System.Text.RegularExpressions;

namespace ValleyVision;

internal sealed record BilibiliVideoLink(string Url, string? Bvid, int Page)
{
    private static readonly Regex VideoPath = new(
        @"^/video/(BV[0-9A-Za-z]{10})/?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ShortPath = new(
        @"^/[0-9A-Za-z]{2,32}/?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryParse(string text, out BilibiliVideoLink? link)
    {
        link = null;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2048 || text != text.Trim()
            || !Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        string host = uri.IdnHost.ToLowerInvariant();
        Match match = VideoPath.Match(uri.AbsolutePath);
        bool isVideoPage = host is "bilibili.com" or "www.bilibili.com" or "m.bilibili.com"
            && match.Success;
        bool isShortLink = host == "b23.tv" && ShortPath.IsMatch(uri.AbsolutePath);
        if (!isVideoPage && !isShortLink)
        {
            return false;
        }

        int page = 1;
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            if (!string.Equals(Uri.UnescapeDataString(parts[0]), "p", StringComparison.Ordinal))
            {
                continue;
            }

            string raw = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (raw.Length is < 1 or > 4 || !int.TryParse(raw, out page) || page < 1)
            {
                return false;
            }
        }

        link = new BilibiliVideoLink(text, isVideoPage ? match.Groups[1].Value : null, page);
        return true;
    }

    internal bool MatchesResolvedPage(string? webpageUrl)
    {
        return webpageUrl is not null
            && TryParse(webpageUrl, out BilibiliVideoLink? resolved)
            && resolved?.Bvid is not null
            && (this.Bvid is null || string.Equals(this.Bvid, resolved.Bvid, StringComparison.OrdinalIgnoreCase));
    }
}
