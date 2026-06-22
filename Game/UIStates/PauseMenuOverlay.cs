using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.UI;
using MyGame.Engine.StandardModules.Multiplayer;
using Steamworks;
using FontStashSharp;
using MyGame.Game.Logic;

namespace MyGame.Game.UIStates;

public enum PauseMenuMode { Main, Session, Save, Load }

public class PauseMenuOverlay
{
    public bool IsPaused { get; private set; } = false;

    private readonly Game1 game;
    private readonly StateManager stateManager;
    private readonly bool isHostOrigin;
    private readonly byte[] signalBuffer = new byte[1];

    private PauseMenuMode currentMode = PauseMenuMode.Main;

    // Main Buttons
    private readonly Button unpauseButton;
    private readonly Button saveButton;
    private readonly Button loadButton;
    private readonly Button hostSessionButton;
    private readonly Button sessionMenuButton;
    private readonly Button optionsButton;
    private readonly Button exitButton;

    // Session Sub-Menu Buttons
    private readonly Button sessionInviteButton;
    private readonly Button sessionCloseLobbyButton;
    private readonly Button sessionBackButton;
    private readonly FriendsListOverlay friendsOverlay;

    // Slot Browser Buttons
    private readonly Button[] slotButtons = new Button[10];
    private readonly Button slotBackButton;

    private string pauseStatusText = "Game Paused";

    public event Action<int>? OnSaveSlotRequested;
    public event Action<int>? OnLoadSlotRequested;

    public PauseMenuOverlay(Game1 game, StateManager stateManager, bool isHostOrigin)
    {
        this.game = game;
        this.stateManager = stateManager;
        this.isHostOrigin = isHostOrigin;

        friendsOverlay = new FriendsListOverlay(game);
        Texture2D uiTex = AssetManager.WhitePixel;

        unpauseButton = new Button(uiTex, Rectangle.Empty) { Text = "Unpause", NormalColor = Color.DarkSlateGray, HoverColor = Color.SlateGray };
        saveButton = new Button(uiTex, Rectangle.Empty) { Text = "Save", NormalColor = Color.DarkCyan, HoverColor = Color.Cyan };
        loadButton = new Button(uiTex, Rectangle.Empty) { Text = "Load", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };

        hostSessionButton = new Button(uiTex, Rectangle.Empty) { Text = "Host Session", NormalColor = Color.DarkGoldenrod, HoverColor = Color.Goldenrod };
        sessionMenuButton = new Button(uiTex, Rectangle.Empty) { Text = "Session Menu", NormalColor = Color.DarkGoldenrod, HoverColor = Color.Goldenrod };
        optionsButton = new Button(uiTex, Rectangle.Empty) { Text = "Options", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        exitButton = new Button(uiTex, Rectangle.Empty) { Text = "Exit to Main Menu", NormalColor = Color.DarkRed, HoverColor = Color.Red };

        sessionInviteButton = new Button(uiTex, Rectangle.Empty) { Text = "Invite Friends", NormalColor = Color.DarkGreen, HoverColor = Color.Green };
        sessionCloseLobbyButton = new Button(uiTex, Rectangle.Empty) { Text = "Close Session", NormalColor = Color.Firebrick, HoverColor = Color.IndianRed };
        sessionBackButton = new Button(uiTex, Rectangle.Empty) { Text = "Back", NormalColor = Color.DarkSlateGray, HoverColor = Color.SlateGray };

        // Initialize Slot Buttons (Zero Allocation)
        for (int i = 0; i < 10; i++)
        {
            int slotId = i + 1;
            slotButtons[i] = new Button(uiTex, Rectangle.Empty) { NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue, FontSize = 16f };
            slotButtons[i].OnClick += () => HandleSlotClick(slotId);
        }
        slotBackButton = new Button(uiTex, Rectangle.Empty) { Text = "Back", NormalColor = Color.DarkRed, HoverColor = Color.Red };
        slotBackButton.OnClick += () => currentMode = PauseMenuMode.Main;

        NetworkRouter.OnPauseStateChanged += HandleNetworkPause;

        // Routing
        unpauseButton.OnClick += () => TransmitPauseState(false);
        saveButton.OnClick += () => { currentMode = PauseMenuMode.Save; RefreshSaveSlots(true); };
        loadButton.OnClick += () => { currentMode = PauseMenuMode.Load; RefreshSaveSlots(false); };
        optionsButton.OnClick += () => stateManager.PushState(new OptionsState(game, stateManager));

        hostSessionButton.OnClick += async () =>
        {
            if (SteamManager.IsSteamActive && !SteamManager.CurrentLobby.HasValue)
            {
                await SteamManager.CreateLobby();
                SteamManager.CurrentLobby?.SetData("GameState", "InGame");
            }
        };

        sessionMenuButton.OnClick += () => currentMode = PauseMenuMode.Session;
        sessionBackButton.OnClick += () => { currentMode = PauseMenuMode.Main; friendsOverlay.IsVisible = false; };
        sessionInviteButton.OnClick += () => friendsOverlay.Show();
        sessionCloseLobbyButton.OnClick += () => { SteamManager.LeaveLobby(); currentMode = PauseMenuMode.Main; };

        exitButton.OnClick += () =>
        {
            SteamManager.LeaveLobby();
            stateManager.ChangeState(new MainMenuState(game, stateManager));
        };
    }

    public void Unload() => NetworkRouter.OnPauseStateChanged -= HandleNetworkPause;

    // Updates text safely, reading DB only ONCE upon opening the tab
    private void RefreshSaveSlots(bool isSaving)
    {
        var profiles = SaveManager.GetDisplayProfiles();
        for (int i = 0; i < 10; i++)
        {
            var p = profiles[i];
            bool isAutoSave = i < 3;

            if (p == null)
            {
                slotButtons[i].Text = isAutoSave ? $"Auto-Save {i+1} - Empty" : $"Slot {i+1} - Empty";
                slotButtons[i].IsEnabled = isSaving && !isAutoSave;
            }
            else
            {
                TimeSpan t = TimeSpan.FromSeconds(p.TotalPlayTimeSeconds);
                string prefix = isAutoSave ? "Auto" : "Slot";
                slotButtons[i].Text = $"{prefix} {i+1} | {p.LastSaved:MM/dd HH:mm} | {t.Hours}h {t.Minutes}m";
                slotButtons[i].IsEnabled = isSaving ? !isAutoSave : true;
            }
        }
    }

    private void HandleSlotClick(int slotId)
    {
        if (currentMode == PauseMenuMode.Save)
        {
            OnSaveSlotRequested?.Invoke(slotId);
            currentMode = PauseMenuMode.Main;
        }
        else if (currentMode == PauseMenuMode.Load)
        {
            OnLoadSlotRequested?.Invoke(slotId);
            TransmitPauseState(false);
        }
    }

    private void HandleNetworkPause(bool state, SteamId senderId)
    {
        IsPaused = state;
        currentMode = PauseMenuMode.Main;

        if (IsPaused && SteamManager.IsSteamActive)
        {
            if (senderId != SteamClient.SteamId) pauseStatusText = $"Game Paused by {new Friend(senderId).Name}";
            else pauseStatusText = "Game Paused";
        }
        else pauseStatusText = "Game Paused";
    }

    public void Update()
    {
        if (InputManager.ConsumeAction(GameActions.Pause)) TransmitPauseState(!IsPaused);

        if (!IsPaused) return;

        Point mousePos = InputManager.GetScreenMousePosition();
        bool isClicked = InputManager.ConsumeUIClick();

        if (currentMode == PauseMenuMode.Session) { UpdateSessionMenu(mousePos, isClicked); return; }
        if (currentMode == PauseMenuMode.Save || currentMode == PauseMenuMode.Load) { UpdateSlotMenu(mousePos, isClicked); return; }

        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 150;
        int currentY = (viewport.Height / 2) - 160;
        int spacing = 55;

        bool isOnline = SteamManager.CurrentLobby.HasValue;

        unpauseButton.Bounds = new Rectangle(centerX, currentY, 300, 45); currentY += spacing;
        unpauseButton.Update(mousePos, isClicked);

        if (isHostOrigin)
        {
            saveButton.Bounds = new Rectangle(centerX, currentY, 300, 45); currentY += spacing;
            saveButton.Update(mousePos, isClicked);
        }

        if (isHostOrigin && !isOnline)
        {
            loadButton.Bounds = new Rectangle(centerX, currentY, 300, 45); currentY += spacing;
            loadButton.Update(mousePos, isClicked);
        }

        if (!isOnline)
        {
            hostSessionButton.Bounds = new Rectangle(centerX, currentY, 300, 45); currentY += spacing;
            hostSessionButton.Update(mousePos, isClicked);
        }
        else
        {
            sessionMenuButton.Bounds = new Rectangle(centerX, currentY, 300, 45); currentY += spacing;
            sessionMenuButton.Update(mousePos, isClicked);
        }

        optionsButton.Bounds = new Rectangle(centerX, currentY, 300, 45); currentY += spacing;
        optionsButton.Update(mousePos, isClicked);

        exitButton.Bounds = new Rectangle(centerX, currentY, 300, 45);
        exitButton.Update(mousePos, isClicked);
    }

    private void UpdateSessionMenu(Point mousePos, bool isClicked)
    {
        friendsOverlay.Update(mousePos, isClicked);
        if (friendsOverlay.IsVisible) return;

        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 150;
        int bottomY = (viewport.Height / 2) + 120;

        if (isHostOrigin)
        {
            sessionInviteButton.Bounds = new Rectangle(centerX, bottomY - 60, 300, 45);
            sessionCloseLobbyButton.Bounds = new Rectangle(centerX, bottomY, 145, 45);
            sessionBackButton.Bounds = new Rectangle(centerX + 155, bottomY, 145, 45);

            sessionInviteButton.Update(mousePos, isClicked);
            sessionCloseLobbyButton.Update(mousePos, isClicked);
        }
        else
        {
            sessionBackButton.Bounds = new Rectangle(centerX, bottomY, 300, 45);
        }

        sessionBackButton.Update(mousePos, isClicked);
    }

    private void UpdateSlotMenu(Point mousePos, bool isClicked)
    {
        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 200;
        int currentY = (viewport.Height / 2) - 260; // Start higher to fit 10 slots

        for (int i = 0; i < 10; i++)
        {
            slotButtons[i].Bounds = new Rectangle(centerX, currentY, 400, 40);
            currentY += 45;
            slotButtons[i].Update(mousePos, isClicked);
        }

        slotBackButton.Bounds = new Rectangle(centerX, currentY + 10, 400, 40);
        slotBackButton.Update(mousePos, isClicked);
    }

    private void TransmitPauseState(bool enforcePause)
    {
        IsPaused = enforcePause;
        pauseStatusText = "Game Paused";
        currentMode = PauseMenuMode.Main;

        signalBuffer[0] = IsPaused ? PacketTypes.PauseGame : PacketTypes.ResumeGame;

        if (SteamManager.CurrentLobby is { } activeLobby)
        {
           foreach (var member in activeLobby.Members)
           {
              if (member.Id != SteamClient.SteamId)
                 SteamNetworking.SendP2PPacket(member.Id, signalBuffer, signalBuffer.Length, 2, P2PSend.Reliable);
           }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsPaused) return;

        var viewport = game.GraphicsDevice.Viewport;
        spriteBatch.Draw(AssetManager.WhitePixel, viewport.Bounds, Color.Black * 0.85f);

        if (AssetManager.IsFontLoaded)
        {
            SpriteFontBase font = AssetManager.GetFont(28f);
            var textSize = font.MeasureString(pauseStatusText);
            System.Numerics.Vector2 textPos = new System.Numerics.Vector2((viewport.Width - textSize.X) / 2f, (viewport.Height / 2f) - 300);
            font.DrawText(AssetManager.FontRenderer, pauseStatusText, textPos, new FSColor(135, 206, 250, 255));
        }

        if (currentMode == PauseMenuMode.Session) { DrawSessionMenu(spriteBatch); return; }
        if (currentMode == PauseMenuMode.Save || currentMode == PauseMenuMode.Load) { DrawSlotMenu(spriteBatch); return; }

        bool isOnline = SteamManager.CurrentLobby.HasValue;

        unpauseButton.Draw(spriteBatch);
        if (isHostOrigin) saveButton.Draw(spriteBatch);
        if (isHostOrigin && !isOnline) loadButton.Draw(spriteBatch);

        if (!isOnline) hostSessionButton.Draw(spriteBatch);
        else sessionMenuButton.Draw(spriteBatch);

        optionsButton.Draw(spriteBatch);
        exitButton.Draw(spriteBatch);
    }

    private void DrawSessionMenu(SpriteBatch spriteBatch)
    {
        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 150;
        int startY = (viewport.Height / 2) - 150;

        if (SteamManager.CurrentLobby is { } activeLobby && AssetManager.IsFontLoaded)
        {
            SpriteFontBase font = AssetManager.GetFont(20f);
            int listY = startY;

            foreach (var member in activeLobby.Members)
            {
                string tag = member.Id == activeLobby.Owner.Id ? "[HOST] " : "[GUEST] ";
                font.DrawText(AssetManager.FontRenderer, tag + member.Name, new System.Numerics.Vector2(centerX, listY), FSColor.White);
                listY += 30;
            }
        }

        if (isHostOrigin)
        {
            sessionInviteButton.Draw(spriteBatch);
            sessionCloseLobbyButton.Draw(spriteBatch);
        }
        sessionBackButton.Draw(spriteBatch);

        friendsOverlay.Draw(spriteBatch);
    }

    private void DrawSlotMenu(SpriteBatch spriteBatch)
    {
        for (int i = 0; i < 10; i++) slotButtons[i].Draw(spriteBatch);
        slotBackButton.Draw(spriteBatch);
    }
}
