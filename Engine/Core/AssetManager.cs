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

    public static string? ResolveAssetPath(string assetPath)
    {
        string localizedPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
        string baseDir = AppContext.BaseDirectory;

        string binPath = Path.GetFullPath(Path.Combine(baseDir, "Content", localizedPath));
        if (File.Exists(binPath)) return binPath;

        string sourcePath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Content", localizedPath));
        if (File.Exists(sourcePath)) return sourcePath;

        // ARCHITECTURE FIX: Return null instead of violently throwing a fatal exception
        return null;
    }

    public static Texture2D GetTexture(string assetPath, bool isMapAsset = false)
    {
        if (Textures.TryGetValue(assetPath, out var existingTex)) return existingTex;

        string? fullPath = ResolveAssetPath(assetPath);

        // ARCHITECTURE FIX: Graceful fallback. Logs to F1 console and returns a white square.
        if (fullPath == null)
        {
            EngineLogger.Log($"Missing texture asset requested: {assetPath}. Injecting fallback WhitePixel.", "ERROR");
            Textures[assetPath] = WhitePixel; // Cache the fallback so we don't spam the hard drive
            return WhitePixel;
        }

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
                // Never dispose the universal WhitePixel fallback
                if (tex != WhitePixel && !tex.IsDisposed) tex.Dispose();
                Textures.Remove(key);
            }
        }
        MapTextureKeys.Clear();
        EngineLogger.Log("Unmanaged Level Textures safely reclaimed.", "SYSTEM");
    }

    public static void LoadFont(string assetPath)
    {
        string? fullPath = ResolveAssetPath(assetPath);
        if (fullPath == null)
        {
            EngineLogger.Log($"Missing font asset requested: {assetPath}.", "ERROR");
            return;
        }

        byte[] ttfData = File.ReadAllBytes(fullPath);
        FontSystem.AddFont(ttfData);
        IsFontLoaded = true;
    }

    public static SpriteFontBase GetFont(float fontSize) => FontSystem.GetFont(fontSize);

    public static string GetTextFile(string assetPath)
    {
        string? fullPath = ResolveAssetPath(assetPath);
        if (fullPath == null)
        {
            EngineLogger.Log($"Missing text file requested: {assetPath}.", "ERROR");
            return string.Empty;
        }
        return File.ReadAllText(fullPath);
    }

    public static void UnloadAll()
    {
        foreach (var tex in Textures.Values)
        {
            if (tex != WhitePixel && !tex.IsDisposed) tex.Dispose();
        }
        Textures.Clear();
        MapTextureKeys.Clear();

        FontSystem.Reset();
        if (WhitePixel != null && !WhitePixel.IsDisposed) WhitePixel.Dispose();
        IsFontLoaded = false;
    }
}
