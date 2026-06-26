using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MyGame.Engine.Core;

public class EngineSettings
{
    public int Id { get; set; }
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public bool Fullscreen { get; set; } = false;
    public bool VSync { get; set; } = true;
    public int TargetFPS { get; set; } = 60; // 0 represents Unlimited
}

public static class SettingsManager
{
    private static Game1 _game = null!;
    private static GraphicsDeviceManager _graphics = null!;

    public static EngineSettings CurrentSettings { get; private set; } = new EngineSettings();

    public static void Initialize(Game1 game, GraphicsDeviceManager graphics)
    {
        _game = game;
        _graphics = graphics;

        // ARCHITECTURE FIX: Severed LiteDB disk dependency for RAM-only dual-instance testing
        CurrentSettings = new EngineSettings { Id = 1 };

        ApplyDisplaySettings(CurrentSettings.Width, CurrentSettings.Height, CurrentSettings.Fullscreen, CurrentSettings.VSync, CurrentSettings.TargetFPS);
    }

    public static DisplayMode[] GetSupportedResolutions()
    {
        return GraphicsAdapter.DefaultAdapter.SupportedDisplayModes
            .Where(m => m.Width >= 1280)
            .OrderBy(m => m.Width)
            .ToArray();
    }

    public static void ApplyDisplaySettings(int width, int height, bool fullscreen, bool vsync, int targetFps)
    {
        CurrentSettings.Width = width;
        CurrentSettings.Height = height;
        CurrentSettings.Fullscreen = fullscreen;
        CurrentSettings.VSync = vsync;
        CurrentSettings.TargetFPS = targetFps;

        bool needsApply = false;

        if (_graphics.PreferredBackBufferWidth != width) { _graphics.PreferredBackBufferWidth = width; needsApply = true; }
        if (_graphics.PreferredBackBufferHeight != height) { _graphics.PreferredBackBufferHeight = height; needsApply = true; }
        if (_graphics.IsFullScreen != fullscreen) { _graphics.IsFullScreen = fullscreen; needsApply = true; }
        if (_graphics.SynchronizeWithVerticalRetrace != vsync) { _graphics.SynchronizeWithVerticalRetrace = vsync; needsApply = true; }

        if (targetFps == 0)
        {
            if (_game.IsFixedTimeStep) { _game.IsFixedTimeStep = false; needsApply = true; }
        }
        else
        {
            var targetSpan = TimeSpan.FromSeconds(1d / targetFps);
            if (!_game.IsFixedTimeStep || _game.TargetElapsedTime != targetSpan)
            {
                _game.IsFixedTimeStep = true;
                _game.TargetElapsedTime = targetSpan;
                needsApply = true;
            }
        }

        if (needsApply)
        {
            _graphics.ApplyChanges();
            EngineLogger.Log($"Graphics Device Context updated to {width}x{height}", "SYSTEM");
        }
    }
}
