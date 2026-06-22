using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using FontStashSharp;

namespace MyGame.Engine.Platform.UI;

public class Button
{
    private readonly Texture2D texture;

    public event Action? OnClick;

    public string Text { get; set; } = string.Empty;
    public Texture2D? Icon { get; set; }
    public bool HasIconSlot { get; set; } = false;

    public float FontSize { get; set; } = 20f;
    public Rectangle Bounds { get; set; }

    public Color NormalColor { get; set; } = Color.DarkSlateBlue;
    public Color HoverColor { get; set; } = Color.SlateBlue;
    public Color TextColor { get; set; } = Color.White;

    public bool IsEnabled { get; set; } = true;
    public bool IsHovered { get; private set; } = false;

    public Button(Texture2D texture, Rectangle bounds)
    {
        this.texture = texture;
        this.Bounds = bounds;
    }

    public void Update(Point mousePosition, bool isClicked)
    {
        if (!IsEnabled) return;

        IsHovered = Bounds.Contains(mousePosition);

        if (IsHovered && isClicked) OnClick?.Invoke();
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        Color btnTint = !IsEnabled ? Color.DimGray : (IsHovered ? HoverColor : NormalColor);
        spriteBatch.Draw(texture, Bounds, btnTint);

        float iconOffset = 0;

        int padding = 4;
        int iconSize = Bounds.Height - (padding * 2);
        Rectangle iconRect = new Rectangle(Bounds.X + padding, Bounds.Y + padding, iconSize, iconSize);

        if (Icon != null && !Icon.IsDisposed)
        {
            spriteBatch.Draw(Icon, iconRect, Color.White);
            iconOffset = iconSize + (padding * 2);
        }
        else if (HasIconSlot)
        {
            spriteBatch.Draw(AssetManager.WhitePixel, iconRect, Color.Black * 0.4f);
            iconOffset = iconSize + (padding * 2);
        }

        if (!string.IsNullOrEmpty(Text) && AssetManager.IsFontLoaded)
        {
           SpriteFontBase font = AssetManager.GetFont(FontSize);

           string displayText = Text;
           float maxTextWidth = Math.Max(10, Bounds.Width - iconOffset - 10f);

           if (font.MeasureString(displayText).X > maxTextWidth)
           {
               while (displayText.Length > 3 && font.MeasureString(displayText + "...").X > maxTextWidth)
               {
                   displayText = displayText.Substring(0, displayText.Length - 1);
               }
               displayText += "...";
           }

           var textSize = font.MeasureString(displayText);

           System.Numerics.Vector2 textPos = new System.Numerics.Vector2(
              Bounds.X + iconOffset + (Bounds.Width - iconOffset - textSize.X) * 0.5f,
              Bounds.Y + (Bounds.Height - textSize.Y) * 0.5f
           );

           Color finalTextColor = !IsEnabled ? Color.Gray : TextColor;

           // ARCHITECTURE FIX: Routed cleanly through custom IFontStashRenderer instance
           font.DrawText(AssetManager.FontRenderer, displayText, textPos, new FSColor(finalTextColor.R, finalTextColor.G, finalTextColor.B, finalTextColor.A));
        }
    }
}
