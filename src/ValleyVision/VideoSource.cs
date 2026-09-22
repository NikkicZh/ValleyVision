namespace ValleyVision;

internal abstract record VideoSource(string Name);
internal sealed record BilibiliSource(BilibiliVideoLink Link) : VideoSource("B 站");
internal sealed record YouTubeSource(YouTubeVideoLink Link) : VideoSource("YouTube");

internal static class VideoSourceRouter
{
    internal static VideoSource? Parse(string text)
    {
        if (BilibiliVideoLink.TryParse(text, out BilibiliVideoLink? bilibili))
            return new BilibiliSource(bilibili!);
        if (YouTubeVideoLink.TryParse(text, out YouTubeVideoLink? youtube))
            return new YouTubeSource(youtube!);
        return null;
    }
}
