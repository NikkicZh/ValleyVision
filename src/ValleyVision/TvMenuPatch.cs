using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Objects;

namespace ValleyVision;

internal sealed class TvMenuPatch
{
    private const string ResponseKey = "ValleyVision.PlayOnlineVideo";
    private const string StopKey = "ValleyVision.StopOnlineVideo";
    private static TvPlaybackController? playback;

    private readonly Harmony harmony;

    internal TvMenuPatch(string harmonyId, TvPlaybackController controller)
    {
        this.harmony = new Harmony(harmonyId);
        playback = controller;
    }

    internal void Register()
    {
        var method = AccessTools.Method(
            typeof(GameLocation),
            nameof(GameLocation.createQuestionDialogue),
            new[] { typeof(string), typeof(Response[]), typeof(GameLocation.afterQuestionBehavior), typeof(NPC) }
        );
        this.harmony.Patch(method, prefix: new HarmonyMethod(typeof(TvMenuPatch), nameof(BeforeQuestionDialogue)));
        this.harmony.Patch(
            AccessTools.Method(typeof(TV), nameof(TV.draw),
                new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
            postfix: new HarmonyMethod(typeof(TvMenuPatch), nameof(AfterDraw)));
    }

    private static void BeforeQuestionDialogue(
        ref Response[] answerChoices,
        ref GameLocation.afterQuestionBehavior afterDialogueBehavior
    )
    {
        if (afterDialogueBehavior?.Target is not TV television
            || afterDialogueBehavior.Method.Name != nameof(TV.selectChannel)
            || answerChoices.Any(choice => choice.responseKey == ResponseKey))
        {
            return;
        }

        int leaveIndex = Array.FindIndex(answerChoices, choice => choice.responseKey == "(Leave)");
        if (leaveIndex < 0)
        {
            return;
        }

        var choices = answerChoices.ToList();
        if (playback?.IsActiveOn(television) == true)
        {
            choices.Insert(leaveIndex, new Response(StopKey, "停止在线视频"));
            leaveIndex++;
        }

        choices.Insert(leaveIndex, new Response(ResponseKey, "播放在线视频"));
        answerChoices = choices.ToArray();

        GameLocation.afterQuestionBehavior original = afterDialogueBehavior;
        afterDialogueBehavior = (who, answer) =>
        {
            if (answer == ResponseKey)
            {
                Game1.afterFadeFunction? previous = Game1.afterDialogues;
                Game1.afterDialogues = () =>
                {
                    previous?.Invoke();
                    Game1.activeClickableMenu = new VideoLinkInputMenu(television,
                        (target, url) => playback?.Start(target, url));
                };
                return;
            }

            if (answer == StopKey)
            {
                playback?.Stop();
                Game1.addHUDMessage(new HUDMessage("在线视频已停止。"));
                return;
            }

            if (answer != "(Leave)" && playback?.IsActiveOn(television) == true)
            {
                playback.Stop();
            }

            original(who, answer);
        };
    }

    private static void AfterDraw(TV __instance, SpriteBatch spriteBatch)
    {
        playback?.Draw(__instance, spriteBatch);
    }
}
