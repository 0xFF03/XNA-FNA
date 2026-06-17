using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Steamworks;

namespace MyGame.Engine.Networking;

public static class SteamAvatarCache
{
    private static readonly Dictionary<SteamId, Texture2D> _cache = new();
    private static readonly HashSet<SteamId> _fetching = new();
    private static readonly HashSet<SteamId> _failedFetches = new(); // ARCHITECTURE FIX: Tracks dead links

    // Thread-safe queue for transferring background image data to the Main Thread
    private static readonly ConcurrentQueue<(SteamId, Steamworks.Data.Image)> _pendingTextures = new();

    public static Texture2D? GetAvatar(SteamId steamId)
    {
        if (_cache.TryGetValue(steamId, out Texture2D? tex)) return tex;
        if (_failedFetches.Contains(steamId)) return null; // Do not infinitely spam Steam API for a broken avatar

        if (!_fetching.Contains(steamId))
        {
            _fetching.Add(steamId);
            FetchAvatarAsync(steamId);
        }
        return null;
    }

    private static async void FetchAvatarAsync(SteamId steamId)
    {
        var image = await SteamFriends.GetMediumAvatarAsync(steamId);
        if (image.HasValue)
        {
            // Send the raw byte data back to the Main Thread
            _pendingTextures.Enqueue((steamId, image.Value));
        }
        else
        {
            _fetching.Remove(steamId);
            _failedFetches.Add(steamId); // Mark as failed so we don't retry every single frame
        }
    }

    public static void Update()
    {
        // Executes strictly on the Main Thread to safely push data to the GPU
        while (_pendingTextures.TryDequeue(out var result))
        {
            var (steamId, img) = result;

            // Facepunch returns RGBA byte arrays, which maps perfectly to FNA's SurfaceFormat.Color
            Texture2D tex = new Texture2D(Game1.Instance.GraphicsDevice, (int)img.Width, (int)img.Height, false, SurfaceFormat.Color);
            tex.SetData(img.Data);

            _cache[steamId] = tex;
            _fetching.Remove(steamId);
        }
    }

    public static void Clear()
    {
        foreach (var tex in _cache.Values)
        {
            if (!tex.IsDisposed) tex.Dispose();
        }
        _cache.Clear();
        _fetching.Clear();
        _failedFetches.Clear();
        _pendingTextures.Clear();
    }
}
