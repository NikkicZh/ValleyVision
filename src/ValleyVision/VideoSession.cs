using System.Diagnostics;

namespace ValleyVision;

internal enum VideoSessionState { Loading, Playing, Ended, Failed, Stopped }

internal sealed class VideoSession : IDisposable
{
    internal const int Width = 168;
    internal const int Height = 112;
    private const int FrameBytes = Width * Height * 3;

    private readonly object gate = new();
    private readonly CancellationTokenSource cancellation = new();
    private Process? videoProcess;
    private Process? audioProcess;
    private byte[]? latestFrame;
    private VideoSessionState state = VideoSessionState.Loading;
    private string? failure;

    internal VideoSession(VideoSource source, ExternalTools tools, ModConfig? config = null)
    {
        _ = Task.Run(() => this.RunAsync(source, tools, config ?? new ModConfig()));
    }

    internal VideoSessionState State
    {
        get { lock (this.gate) { return this.state; } }
    }

    internal string? Failure
    {
        get { lock (this.gate) { return this.failure; } }
    }

    internal byte[]? LatestFrame
    {
        get { lock (this.gate) { return this.latestFrame; } }
    }

    internal void Stop()
    {
        this.cancellation.Cancel();
        lock (this.gate)
        {
            Kill(this.videoProcess);
            Kill(this.audioProcess);
            this.latestFrame = null;
            this.state = VideoSessionState.Stopped;
        }
    }

    public void Dispose()
    {
        this.Stop();
    }

    private async Task RunAsync(VideoSource source, ExternalTools tools, ModConfig config)
    {
        Process? video = null;
        Process? audio = null;
        try
        {
            ResolvedMedia media = source switch
            {
                BilibiliSource bilibili => await BilibiliMediaResolver.ResolveAsync(
                    bilibili.Link, tools.YtDlp, this.cancellation.Token),
                YouTubeSource youtube => await YouTubeMediaResolver.ResolveAsync(
                    youtube.Link, tools.YtDlp, config, this.cancellation.Token),
                _ => throw new MediaException("当前来源未支持。", MediaError.Unsupported)
            };
            this.cancellation.Token.ThrowIfCancellationRequested();

            video = StartVideo(tools.Ffmpeg, media);
            this.cancellation.Token.ThrowIfCancellationRequested();
            audio = StartAudio(tools.Ffplay, media);
            lock (this.gate)
            {
                this.videoProcess = video;
                this.audioProcess = audio;
                if (this.cancellation.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
            }

            _ = DrainErrorsAsync(video.StandardError);
            _ = DrainErrorsAsync(audio.StandardError);
            var frame = new byte[FrameBytes];
            while (!this.cancellation.IsCancellationRequested)
            {
                if (audio.HasExited)
                {
                    throw new MediaException("视频声音已中断。", MediaError.Unplayable);
                }

                int filled = 0;
                while (filled < FrameBytes)
                {
                    if (audio.HasExited)
                    {
                        throw new MediaException("视频声音已中断。", MediaError.Unplayable);
                    }

                    using var poll = CancellationTokenSource.CreateLinkedTokenSource(this.cancellation.Token);
                    poll.CancelAfter(TimeSpan.FromMilliseconds(500));
                    int read;
                    try
                    {
                        read = await video.StandardOutput.BaseStream.ReadAsync(
                            frame.AsMemory(filled, FrameBytes - filled), poll.Token);
                    }
                    catch (OperationCanceledException) when (!this.cancellation.IsCancellationRequested)
                    {
                        continue;
                    }
                    if (read == 0)
                    {
                        if (filled != 0)
                        {
                            throw new MediaException("视频画面不完整。", MediaError.Unplayable);
                        }

                        lock (this.gate)
                        {
                            if (this.cancellation.IsCancellationRequested) return;
                            this.state = this.latestFrame is null
                                ? VideoSessionState.Failed : VideoSessionState.Ended;
                            if (this.latestFrame is null)
                            {
                                this.failure = "视频没有可用画面。";
                            }
                        }

                        return;
                    }

                    filled += read;
                }

                lock (this.gate)
                {
                    if (this.cancellation.IsCancellationRequested) return;
                    this.latestFrame = (byte[])frame.Clone();
                    this.state = VideoSessionState.Playing;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stop owns the visible state.
        }
        catch (MediaException exception)
        {
            this.Fail(exception.Message);
        }
        catch (Exception)
        {
            this.Fail("视频播放失败；请检查链接与外部工具。");
        }
        finally
        {
            Kill(video);
            Kill(audio);
            video?.Dispose();
            audio?.Dispose();
            lock (this.gate)
            {
                this.videoProcess = null;
                this.audioProcess = null;
            }
        }
    }

    private void Fail(string message)
    {
        lock (this.gate)
        {
            if (this.state == VideoSessionState.Stopped)
            {
                return;
            }

            this.failure = message;
            this.state = VideoSessionState.Failed;
        }
    }

    private static Process StartVideo(string executable, ResolvedMedia media)
    {
        var process = Create(executable, redirectOutput: true);
        AddHeaders(process, media.Video);
        foreach (string argument in new[]
        {
            "-nostdin", "-loglevel", "error", "-reconnect", "1", "-reconnect_streamed", "1",
            "-reconnect_delay_max", "2", "-re", "-i", media.Video.Url, "-map", "0:v:0", "-an",
            "-vf", $"scale={Width}:{Height},fps=12", "-pix_fmt", "bgr24", "-f", "rawvideo", "pipe:1"
        })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        return process;
    }

    private static Process StartAudio(string executable, ResolvedMedia media)
    {
        var process = Create(executable, redirectOutput: false);
        foreach (string argument in new[] { "-nodisp", "-autoexit", "-loglevel", "error" })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        AddHeaders(process, media.Audio);
        process.StartInfo.ArgumentList.Add(media.Audio.Url);
        process.Start();
        return process;
    }

    private static Process Create(string executable, bool redirectOutput)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = true
            }
        };
    }

    private static void AddHeaders(Process process, MediaStream media)
    {
        if (media.UserAgent.Length > 0)
        {
            process.StartInfo.ArgumentList.Add("-user_agent");
            process.StartInfo.ArgumentList.Add(media.UserAgent);
        }

        if (media.Referer.Length > 0)
        {
            process.StartInfo.ArgumentList.Add("-headers");
            process.StartInfo.ArgumentList.Add($"Referer: {media.Referer}\r\n");
        }
    }

    private static void Kill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static async Task DrainErrorsAsync(StreamReader reader)
    {
        try
        {
            var buffer = new char[2048];
            while (await reader.ReadAsync(buffer.AsMemory()) > 0) { }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }
}
