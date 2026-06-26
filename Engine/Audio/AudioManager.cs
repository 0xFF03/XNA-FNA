using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.IO;

namespace MyGame.Engine.Core;

public static class AudioManager
{
    private static readonly Dictionary<string, SoundEffect> SoundCache = new();
    private static readonly Dictionary<string, Song> MusicCache = new();

    private static readonly Queue<string> SoundOrderQueue = new();

    private const int MaxCachedSounds = 64;

    public static float MasterVolume { get; set; } = 1.0f;
    public static float MusicVolume { get; set; } = 0.5f;
    public static float SfxVolume { get; set; } = 0.8f;

    public static void Initialize()
    {
        MediaPlayer.Volume = MasterVolume * MusicVolume;
        SoundEffect.MasterVolume = MasterVolume * SfxVolume;
        EngineLogger.Log("Audio subsystem initialized.", "SYSTEM");
    }

    public static void PlaySound(string assetName)
    {
        if (!SoundCache.TryGetValue(assetName, out SoundEffect? sfx))
        {
            if (SoundCache.Count >= MaxCachedSounds)
            {
                string oldestKey = SoundOrderQueue.Dequeue();
                if (SoundCache.TryGetValue(oldestKey, out var oldSfx))
                {
                    oldSfx.Dispose();
                    SoundCache.Remove(oldestKey);
                }
            }

            string? fullPath = AssetManager.ResolveAssetPath(assetName);
            if (fullPath == null)
            {
                EngineLogger.Log($"Missing audio asset requested: {assetName}.", "ERROR");
                return;
            }

            using var stream = File.OpenRead(fullPath);
            sfx = SoundEffect.FromStream(stream);

            SoundCache[assetName] = sfx;
            SoundOrderQueue.Enqueue(assetName);
        }

        sfx?.Play(MasterVolume * SfxVolume, 0f, 0f);
    }

    public static void PlayMusic(string assetName, bool loop = true)
    {
        EngineLogger.Log($"Request to play music -> {assetName}", "SYSTEM");
    }

    public static void UnloadAll()
    {
        foreach (var sfx in SoundCache.Values) if (!sfx.IsDisposed) sfx.Dispose();
        foreach (var song in MusicCache.Values) if (!song.IsDisposed) song.Dispose();
        SoundCache.Clear();
        SoundOrderQueue.Clear();
        MusicCache.Clear();
    }
}
