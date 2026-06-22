using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.UI;
using MyGame.Game.Logic;

namespace MyGame.Game.UIStates;

public class OptionsState : GameState
{
    private Button resButton = null!;
    private Button fpsButton = null!;
    private Button vsyncButton = null!;
    private Button fullscreenButton = null!;
    private Button applyButton = null!;
    private Button wipeSavesButton = null!;
    private Button backButton = null!;

    private DisplayMode[] _resolutions = null!;
    private int _currentResIndex;
    private readonly int[] _fpsOptions = { 30, 60, 120, 240, 0 };
    private int _currentFpsIndex;
    private bool _pendingFullscreen;
    private bool _pendingVSync;

    public OptionsState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

    public override void LoadContent()
    {
        Texture2D uiTex = AssetManager.WhitePixel;

        _resolutions = SettingsManager.GetSupportedResolutions();
        _pendingFullscreen = SettingsManager.CurrentSettings.Fullscreen;
        _pendingVSync = SettingsManager.CurrentSettings.VSync;

        _currentResIndex = Array.FindIndex(_resolutions, r => r.Width == SettingsManager.CurrentSettings.Width && r.Height == SettingsManager.CurrentSettings.Height);
        if (_currentResIndex == -1) _currentResIndex = 0;

        _currentFpsIndex = Array.IndexOf(_fpsOptions, SettingsManager.CurrentSettings.TargetFPS);
        if (_currentFpsIndex == -1) _currentFpsIndex = 1;

        resButton = new Button(uiTex, Rectangle.Empty) { NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        fpsButton = new Button(uiTex, Rectangle.Empty) { NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        vsyncButton = new Button(uiTex, Rectangle.Empty) { NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        fullscreenButton = new Button(uiTex, Rectangle.Empty) { NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };

        applyButton = new Button(uiTex, Rectangle.Empty) { Text = "Apply Settings", NormalColor = Color.DarkGreen, HoverColor = Color.Green };
        wipeSavesButton = new Button(uiTex, Rectangle.Empty) { Text = "Wipe All Save Data", NormalColor = Color.Firebrick, HoverColor = Color.IndianRed };
        backButton = new Button(uiTex, Rectangle.Empty) { Text = "Back", NormalColor = Color.DarkRed, HoverColor = Color.Red };

        resButton.OnClick += () => { _currentResIndex = (_currentResIndex + 1) % _resolutions.Length; };
        fpsButton.OnClick += () => { _currentFpsIndex = (_currentFpsIndex + 1) % _fpsOptions.Length; };
        vsyncButton.OnClick += () => { _pendingVSync = !_pendingVSync; };
        fullscreenButton.OnClick += () => { _pendingFullscreen = !_pendingFullscreen; };

        applyButton.OnClick += () =>
        {
            var res = _resolutions[_currentResIndex];
            int fps = _fpsOptions[_currentFpsIndex];
            SettingsManager.ApplyDisplaySettings(res.Width, res.Height, _pendingFullscreen, _pendingVSync, fps);
        };

        wipeSavesButton.OnClick += () => SaveManager.DeleteAllSaves();

        backButton.OnClick += () => { stateManager.PopState(); };

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        var res = _resolutions[_currentResIndex];
        resButton.Text = $"Resolution: {res.Width}x{res.Height}";

        int fps = _fpsOptions[_currentFpsIndex];
        fpsButton.Text = fps == 0 ? "Framerate: Unlimited" : $"Framerate: {fps} FPS";

        vsyncButton.Text = _pendingVSync ? "VSync: ON" : "VSync: OFF";
        fullscreenButton.Text = _pendingFullscreen ? "Fullscreen: ON" : "Fullscreen: OFF";
    }

    public override void Update(float deltaTime)
    {
        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 150;
        int startY = (viewport.Height / 2) - 200;
        int spacing = 55;

        resButton.Bounds = new Rectangle(centerX, startY, 300, 45);
        fpsButton.Bounds = new Rectangle(centerX, startY + spacing, 300, 45);
        vsyncButton.Bounds = new Rectangle(centerX, startY + spacing * 2, 300, 45);
        fullscreenButton.Bounds = new Rectangle(centerX, startY + spacing * 3, 300, 45);
        applyButton.Bounds = new Rectangle(centerX, startY + spacing * 4 + 20, 300, 45);
        wipeSavesButton.Bounds = new Rectangle(centerX, startY + spacing * 5 + 20, 300, 45);
        backButton.Bounds = new Rectangle(centerX, startY + spacing * 6 + 20, 300, 45);

        Point mousePos = InputManager.GetScreenMousePosition();
        bool isClicked = InputManager.ConsumeUIClick();

        resButton.Update(mousePos, isClicked);
        fpsButton.Update(mousePos, isClicked);
        vsyncButton.Update(mousePos, isClicked);
        fullscreenButton.Update(mousePos, isClicked);
        applyButton.Update(mousePos, isClicked);
        wipeSavesButton.Update(mousePos, isClicked);
        backButton.Update(mousePos, isClicked);

        UpdateLabels();
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        game.GraphicsDevice.Clear(Color.DimGray);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

        resButton.Draw(spriteBatch);
        fpsButton.Draw(spriteBatch);
        vsyncButton.Draw(spriteBatch);
        fullscreenButton.Draw(spriteBatch);
        applyButton.Draw(spriteBatch);
        wipeSavesButton.Draw(spriteBatch);
        backButton.Draw(spriteBatch);

        spriteBatch.End();
    }
}
