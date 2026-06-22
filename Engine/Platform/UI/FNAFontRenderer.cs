using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using FontStashSharp.Interfaces;
using Texture2D = Microsoft.Xna.Framework.Graphics.Texture2D;
using SysRectangle = System.Drawing.Rectangle;
using SysPoint = System.Drawing.Point;
using NumVector2 = System.Numerics.Vector2;

namespace MyGame.Engine.Platform.UI;

public class FNAFontRenderer : IFontStashRenderer
{
    private readonly SpriteBatch _spriteBatch;
    public ITexture2DManager TextureManager { get; }

    public FNAFontRenderer(SpriteBatch spriteBatch)
    {
        _spriteBatch = spriteBatch;
        TextureManager = new FNAPlatformTextureManager(spriteBatch.GraphicsDevice);
    }

    public void Draw(object texture, NumVector2 position, SysRectangle? srcRect, FSColor color, float rotation, NumVector2 origin, float depth)
    {
        var xnaTexture = (Texture2D)texture;

        Vector2 xnaPos = new Vector2(position.X, position.Y);
        Vector2 xnaOrigin = new Vector2(origin.X, origin.Y);
        Color xnaColor = new Color(color.R, color.G, color.B, color.A);

        Rectangle? xnaSrcRect = null;
        if (srcRect.HasValue)
        {
            xnaSrcRect = new Rectangle(srcRect.Value.X, srcRect.Value.Y, srcRect.Value.Width, srcRect.Value.Height);
        }

        _spriteBatch.Draw(xnaTexture, xnaPos, xnaSrcRect, xnaColor, rotation, xnaOrigin, Vector2.One, SpriteEffects.None, depth);
    }
}

internal class FNAPlatformTextureManager : ITexture2DManager
{
    private readonly GraphicsDevice _graphicsDevice;

    public FNAPlatformTextureManager(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public object CreateTexture(int width, int height)
    {
        return new Texture2D(_graphicsDevice, width, height);
    }

    public SysPoint GetTextureSize(object texture)
    {
        var xnaTexture = (Texture2D)texture;
        return new SysPoint(xnaTexture.Width, xnaTexture.Height);
    }

    public void SetTextureData(object texture, SysRectangle bounds, byte[] data)
    {
        var xnaTexture = (Texture2D)texture;

        Rectangle xnaBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        xnaTexture.SetData(0, xnaBounds, data, 0, data.Length);
    }
}
