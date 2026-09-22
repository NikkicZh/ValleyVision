using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;

namespace ValleyVision;

internal sealed class TvPlaybackController : IDisposable
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly ModConfig config;
    private TV? target;
    private VideoSession? session;
    private Texture2D? texture;
    private Color[]? pixels;
    private byte[]? uploadedFrame;
    private bool reportedPlaying;
    private string sourceName = string.Empty;
    private DateTime startedAtUtc;

    internal TvPlaybackController(IModHelper helper, IMonitor monitor, ModConfig config)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.config = config;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.Player.Warped += this.OnWarped;
    }

    internal bool IsActiveOn(TV television)
    {
        return ReferenceEquals(this.target, television) && this.session is not null;
    }

    internal void Start(TV television, string text)
    {
        VideoSource? source = VideoSourceRouter.Parse(text);
        if (source is null)
        {
            Show("当前来源或视频链接未支持。请选择 B 站 BV、b23.tv 或 YouTube 普通视频。");
            return;
        }

        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, television.Location))
        {
            Show("电视已不在当前场景，请重新交互。");
            return;
        }

        this.Stop();
        if (!ExternalTools.TryResolve(this.config, out ExternalTools? tools, out string error))
        {
            Show(error);
            return;
        }

        this.target = television;
        this.session = new VideoSession(source, tools!, this.config);
        this.sourceName = source.Name;
        this.reportedPlaying = false;
        this.startedAtUtc = DateTime.UtcNow;
        Show($"正在加载{source.Name}视频…");
    }

    internal void Stop()
    {
        this.session?.Dispose();
        this.session = null;
        this.target = null;
        this.reportedPlaying = false;
        this.sourceName = string.Empty;
        this.startedAtUtc = default;
        this.uploadedFrame = null;
        this.texture?.Dispose();
        this.texture = null;
        this.pixels = null;
    }

    internal void Draw(TV television, SpriteBatch spriteBatch)
    {
        if (!this.IsActiveOn(television) || this.session?.LatestFrame is not byte[] frame
            || !TryGetScreen(television, out Rectangle screen, out float depth))
        {
            return;
        }

        if (this.texture is null || this.texture.IsDisposed)
        {
            this.texture = new Texture2D(Game1.graphics.GraphicsDevice, VideoSession.Width, VideoSession.Height);
            this.pixels = new Color[VideoSession.Width * VideoSession.Height];
        }

        if (!ReferenceEquals(this.uploadedFrame, frame) && this.pixels is not null)
        {
            for (int index = 0; index < this.pixels.Length; index++)
            {
                int offset = index * 3;
                this.pixels[index] = new Color(frame[offset + 2], frame[offset + 1], frame[offset]);
            }

            this.texture.SetData(this.pixels);
            this.uploadedFrame = frame;
        }

        Vector2 position = Game1.GlobalToLocal(new Vector2(screen.X, screen.Y));
        spriteBatch.Draw(this.texture,
            new Rectangle((int)position.X, (int)position.Y, screen.Width, screen.Height),
            null, Color.White, 0f, Vector2.Zero, SpriteEffects.None, depth + 0.0002f);
    }

    public void Dispose()
    {
        this.Stop();
        this.helper.Events.GameLoop.UpdateTicked -= this.OnUpdateTicked;
        this.helper.Events.GameLoop.ReturnedToTitle -= this.OnReturnedToTitle;
        this.helper.Events.Player.Warped -= this.OnWarped;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (this.session is null)
        {
            return;
        }

        if (!Context.IsWorldReady || this.target?.Location is not GameLocation location
            || !ReferenceEquals(location, Game1.currentLocation)
            || !location.furniture.Any(furniture => ReferenceEquals(furniture, this.target)))
        {
            this.Stop();
            return;
        }

        switch (this.session.State)
        {
            case VideoSessionState.Loading when DateTime.UtcNow - this.startedAtUtc > TimeSpan.FromSeconds(35):
                this.Stop();
                Show("视频开播超时；请检查网络和工具配置。");
                break;
            case VideoSessionState.Playing when !this.reportedPlaying:
                this.reportedPlaying = true;
                Show($"{this.sourceName}视频已开始播放。");
                break;
            case VideoSessionState.Failed:
                string message = this.session.Failure ?? "视频播放失败。";
                this.monitor.Log(message, LogLevel.Warn);
                this.Stop();
                Show(message);
                break;
            case VideoSessionState.Ended:
                this.Stop();
                Show("视频播放结束。");
                break;
        }
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e) => this.Stop();

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.IsLocalPlayer)
        {
            this.Stop();
        }
    }

    private static void Show(string message)
    {
        Game1.addHUDMessage(new HUDMessage(message));
    }

    private static bool TryGetScreen(TV television, out Rectangle screen, out float depth)
    {
        screen = Rectangle.Empty;
        depth = 0;
        string id = television.QualifiedItemId;
        float scale = id is "(F)1468" or "(F)2326" ? 4f : 2f;
        Rectangle bounds = television.boundingBox.Value;
        Vector2 origin = id switch
        {
            "(F)1466" => new Vector2(bounds.X + 24, bounds.Y),
            "(F)1468" => new Vector2(bounds.X + 12, bounds.Y - 96),
            "(F)2326" => new Vector2(bounds.X + 12, bounds.Y - 88),
            "(F)1680" => new Vector2(bounds.X + 24, bounds.Y - 12),
            "(F)RetroTV" => new Vector2(bounds.X + 24, bounds.Y - 64),
            _ => Vector2.Zero
        };

        if (origin == Vector2.Zero)
        {
            return false;
        }

        screen = new Rectangle((int)origin.X, (int)origin.Y,
            (int)(42 * scale), (int)(28 * scale));
        depth = (bounds.Bottom - 1) / 10000f + 1E-05f;
        return true;
    }
}
