using System.Diagnostics;
using System.Text;

namespace ValleyVision;

internal static class YtDlpRunner
{
    internal static async Task<(string Json, string Error, int ExitCode)> RunAsync(
        string executable, IEnumerable<string> arguments, string url, CancellationToken cancellationToken,
        TimeSpan? timeoutOverride = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executable, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.StartInfo.ArgumentList.Add(url);
        if (!process.Start()) throw new MediaException("无法启动 yt-dlp。", MediaError.Tool);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutOverride ?? TimeSpan.FromSeconds(25));
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;
        try
        {
            stdoutTask = ReadBoundedAsync(process.StandardOutput, 2_000_000, timeout.Token);
            stderrTask = ReadBoundedAsync(process.StandardError, 32_000, timeout.Token, truncate: true);
            Task wait = process.WaitForExitAsync(timeout.Token);
            Task first = await Task.WhenAny(wait, stdoutTask);
            if (ReferenceEquals(first, stdoutTask)) await stdoutTask;
            await wait;
            return (await stdoutTask, await stderrTask, process.ExitCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MediaException("视频解析超时。", MediaError.Timeout);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            if (stdoutTask is not null) { try { await stdoutTask; } catch (Exception) { } }
            if (stderrTask is not null) { try { await stderrTask; } catch (Exception) { } }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token, bool truncate = false)
    {
        var result = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(), token);
            if (count == 0) return result.ToString();
            if (result.Length + count > limit)
            {
                if (!truncate) throw new MediaException("视频解析结果过大。", MediaError.Unplayable);
                continue;
            }
            result.Append(buffer, 0, count);
        }
    }
}
