namespace ValleyVision;

internal sealed record MediaStream(string Url, string UserAgent, string Referer);
internal sealed record ResolvedMedia(MediaStream Video, MediaStream Audio);
