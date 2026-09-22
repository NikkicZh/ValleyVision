using StardewModdingAPI;

namespace ValleyVision;

internal sealed class ModEntry : Mod
{
    private TvPlaybackController? playback;

    public override void Entry(IModHelper helper)
    {
        this.Monitor.Log("ValleyVision 已加载。", LogLevel.Info);
        ModConfig config = helper.ReadConfig<ModConfig>();
        this.playback = new TvPlaybackController(helper, this.Monitor, config);
        new TvMenuPatch(this.ModManifest.UniqueID, this.playback).Register();
    }
}
