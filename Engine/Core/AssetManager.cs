using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using MyGame.Engine.UI;

namespace MyGame.Engine.Core;

public static class AssetManager
{
    private static GraphicsDevice _graphicsDevice = null!;
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static readonly FontSystem FontSystem = new();

    public static Texture2D WhitePixel { get; private set; } = null!;
    public static bool IsFontLoaded { get; private set; } = false;

    public static FNAFontRenderer FontRenderer { get; private set; } = null!;

    public static void Initialize(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        WhitePixel = new Texture2D(_graphicsDevice, 1, 1);
        WhitePixel.SetData(new[] { Color.White });

        FontRenderer = new FNAFontRenderer(spriteBatch);
    }

    public static Texture2D GetTexture(string assetPath)
    {
        if (Textures.TryGetValue(assetPath, out var existingTex)) return existingTex;
        string fullPath = Path.Combine("Content", assetPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"[AssetManager]: Missing texture at {fullPath}");
        using var stream = File.OpenRead(fullPath);
        var newTex = Texture2D.FromStream(_graphicsDevice, stream);
        Textures[assetPath] = newTex;
        return newTex;
    }

    public static void LoadFont(string assetPath)
    {
        string fullPath = Path.Combine("Content", assetPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"[AssetManager]: Missing font at {fullPath}");
        byte[] ttfData = File.ReadAllBytes(fullPath);
        FontSystem.AddFont(ttfData);
        IsFontLoaded = true;
    }

    public static SpriteFontBase GetFont(float fontSize) => FontSystem.GetFont(fontSize);

    public static string GetTextFile(string assetPath)
    {
        string fullPath = Path.Combine("Content", assetPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"[AssetManager]: Missing data file at {fullPath}");
        return File.ReadAllText(fullPath);
    }

    public static void UnloadAll()
    {
        // ARCHITECTURE FIX: Added IsDisposed checks to prevent runtime exceptions
        foreach (var tex in Textures.Values)
        {
            if (!tex.IsDisposed) tex.Dispose();
        }
        Textures.Clear();

        FontSystem.Reset();

        if (WhitePixel != null && !WhitePixel.IsDisposed) WhitePixel.Dispose();
        IsFontLoaded = false;
    }
}
