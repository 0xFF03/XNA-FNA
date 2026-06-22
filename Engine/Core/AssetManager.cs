using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using MyGame.Engine.Platform.UI;

namespace MyGame.Engine.Core;

public static class AssetManager
{
    private static GraphicsDevice _graphicsDevice = null!;
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static readonly FontSystem FontSystem = new();
    private static readonly List<string> MapTextureKeys = new();

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

    public static string ResolveAssetPath(string assetPath)
    {
        string localizedPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
        string baseDir = AppContext.BaseDirectory;

        string binPath = Path.GetFullPath(Path.Combine(baseDir, "Content", localizedPath));
        if (File.Exists(binPath)) return binPath;

        string sourcePath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Content", localizedPath));
        if (File.Exists(sourcePath)) return sourcePath;

        throw new FileNotFoundException($"[AssetManager]: Missing asset! Checked both runtime and source directories:\nBin: {binPath}\nSource: {sourcePath}");
    }

    public static Texture2D GetTexture(string assetPath, bool isMapAsset = false)
    {
        if (Textures.TryGetValue(assetPath, out var existingTex)) return existingTex;

        string fullPath = ResolveAssetPath(assetPath);
        using var stream = File.OpenRead(fullPath);
        var newTex = Texture2D.FromStream(_graphicsDevice, stream);
        Textures[assetPath] = newTex;

        if (isMapAsset && !MapTextureKeys.Contains(assetPath))
        {
            MapTextureKeys.Add(assetPath);
        }

        return newTex;
    }

    public static void UnloadLevelAssets()
    {
        foreach (var key in MapTextureKeys)
        {
            if (Textures.TryGetValue(key, out var tex))
            {
                if (!tex.IsDisposed) tex.Dispose();
                Textures.Remove(key);
            }
        }
        MapTextureKeys.Clear();
        Console.WriteLine("[AssetManager]: Unmanaged Level Textures safely reclaimed.");
    }

    public static void LoadFont(string assetPath)
    {
        string fullPath = ResolveAssetPath(assetPath);
        byte[] ttfData = File.ReadAllBytes(fullPath);
        FontSystem.AddFont(ttfData);
        IsFontLoaded = true;
    }

    public static SpriteFontBase GetFont(float fontSize) => FontSystem.GetFont(fontSize);

    public static string GetTextFile(string assetPath) => File.ReadAllText(ResolveAssetPath(assetPath));

    public static void UnloadAll()
    {
        foreach (var tex in Textures.Values)
        {
            if (!tex.IsDisposed) tex.Dispose();
        }
        Textures.Clear();
        MapTextureKeys.Clear();

        FontSystem.Reset();
        if (WhitePixel != null && !WhitePixel.IsDisposed) WhitePixel.Dispose();
        IsFontLoaded = false;
    }
}
