using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace ValleyVision;

internal sealed class VideoLinkInputMenu : IClickableMenu
{
    private const int MaximumLinkLength = 2048;

    private readonly LinkTextBox linkInput;
    private readonly TV television;
    private readonly Action<TV, string> onSubmit;
    private Rectangle confirmButton;
    private Rectangle cancelButton;
    private string? error;

    internal VideoLinkInputMenu(TV television, Action<TV, string> onSubmit)
        : base(0, 0, 0, 0)
    {
        this.television = television;
        this.onSubmit = onSubmit;
        this.linkInput = new LinkTextBox(
            Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
            Game1.smallFont,
            Game1.textColor
        )
        {
            textLimit = MaximumLinkLength + 1,
            limitWidth = false
        };
        this.linkInput.OnEnterPressed += _ => this.Confirm();
        this.Reposition();
        this.linkInput.Selected = true;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.confirmButton.Contains(x, y))
        {
            this.Confirm();
        }
        else if (this.cancelButton.Contains(x, y))
        {
            this.exitThisMenu();
        }
        else if (new Rectangle(this.linkInput.X, this.linkInput.Y, this.linkInput.Width, this.linkInput.Height).Contains(x, y))
        {
            this.linkInput.Selected = true;
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            this.exitThisMenu();
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.Reposition();
    }

    protected override void cleanupBeforeExit()
    {
        this.linkInput.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == this.linkInput)
        {
            Game1.keyboardDispatcher.Subscriber = null;
        }

        base.cleanupBeforeExit();
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,
            new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.65f);
        drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
        Utility.drawTextWithShadow(b, "播放在线视频", Game1.dialogueFont,
            new Vector2(this.xPositionOnScreen + 48, this.yPositionOnScreen + 44), Game1.textColor);
        Utility.drawTextWithShadow(b, "请输入视频链接：", Game1.smallFont,
            new Vector2(this.xPositionOnScreen + 48, this.yPositionOnScreen + 110), Game1.textColor);
        this.linkInput.Draw(b);

        if (this.error is not null)
        {
            Utility.drawTextWithShadow(b, this.error, Game1.smallFont,
                new Vector2(this.xPositionOnScreen + 48, this.yPositionOnScreen + 208), Color.DarkRed);
        }

        this.DrawButton(b, this.confirmButton, "确认");
        this.DrawButton(b, this.cancelButton, "取消");
        this.drawMouse(b);
    }

    private static bool IsValidLink(string text)
    {
        string trimmed = text.Trim();
        return text.Length <= MaximumLinkLength
            && trimmed.Length > 0
            && Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrWhiteSpace(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo);
    }

    private void Confirm()
    {
        if (!IsValidLink(this.linkInput.Text))
        {
            this.error = "请输入有效的 HTTPS 链接（不超过 2048 字符）。";
            this.linkInput.Selected = true;
            return;
        }

        this.exitThisMenu();
        this.onSubmit(this.television, this.linkInput.Text.Trim());
    }

    private void Reposition()
    {
        this.width = Math.Min(960, Game1.uiViewport.Width - 64);
        this.height = 360;
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;
        this.linkInput.X = this.xPositionOnScreen + 48;
        this.linkInput.Y = this.yPositionOnScreen + 150;
        this.linkInput.Width = this.width - 96;
        this.confirmButton = new Rectangle(this.xPositionOnScreen + this.width - 320,
            this.yPositionOnScreen + 272, 120, 56);
        this.cancelButton = new Rectangle(this.xPositionOnScreen + this.width - 176,
            this.yPositionOnScreen + 272, 120, 56);
    }

    private void DrawButton(SpriteBatch b, Rectangle bounds, string label)
    {
        drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White);
        Vector2 size = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2), Game1.textColor);
    }

    private sealed class LinkTextBox : TextBox
    {
        internal LinkTextBox(Texture2D texture, SpriteFont font, Color textColor)
            : base(texture, null, font, textColor)
        {
        }

        public override void RecieveTextInput(string text)
        {
            int remaining = MaximumLinkLength + 1 - this.Text.Length;
            if (remaining > 0)
            {
                base.RecieveTextInput(text[..Math.Min(remaining, text.Length)]);
            }
        }
    }
}
