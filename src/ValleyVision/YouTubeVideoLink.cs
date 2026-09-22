using System.Text.RegularExpressions;

namespace ValleyVision;

internal sealed record YouTubeVideoLink(string VideoId, string Url)
{
    private static readonly Regex IdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryParse(string text, out YouTubeVideoLink? link)
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
        string id = string.Empty;
        if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            if (uri.AbsolutePath != "/watch") return false;
            foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split('=', 2);
                if (Uri.UnescapeDataString(parts[0]) != "v") continue;
                if (id.Length > 0 || parts.Length != 2) return false;
                id = Uri.UnescapeDataString(parts[1]);
            }
        }
        else if (host == "youtu.be")
        {
            string path = uri.AbsolutePath.Trim('/');
            if (path.Contains('/')) return false;
            id = Uri.UnescapeDataString(path);
        }
        else return false;

        if (!IdPattern.IsMatch(id)) return false;
        link = new YouTubeVideoLink(id, $"https://www.youtube.com/watch?v={id}");
        return true;
    }
}
