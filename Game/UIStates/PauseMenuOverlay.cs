using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.UI;
using MyGame.Engine.Core;
using MyGame.Engine.Networking;
using MyGame.Engine.Input;
using Steamworks;
using FontStashSharp;

namespace MyGame.Game.UIStates;

public class PauseMenuOverlay
{
    public bool IsPaused { get; private set; } = false;
    private int previousMemberCount = 0;

    private readonly Game1 game;
    private readonly StateManager stateManager;
    private readonly bool isHost;
    private readonly byte[] signalBuffer = new byte[1];

    private readonly Button continueButton;
    private readonly Button inviteButton;
    private readonly Button closeLobbyButton;
    private readonly Button exitButton;

    private readonly FriendsListOverlay friendsOverlay;
    private string networkStatusText = "Unknown Status";

    public PauseMenuOverlay(Game1 game, StateManager stateManager, bool isHost)
    {
        this.game = game;
        this.stateManager = stateManager;
        this.isHost = isHost;

        friendsOverlay = new FriendsListOverlay(game);
        Texture2D uiTex = AssetManager.WhitePixel;

        continueButton = new Button(uiTex, Rectangle.Empty) { Text = "Continue", NormalColor = Color.DarkSlateGray, HoverColor = Color.SlateGray };
        inviteButton = new Button(uiTex, Rectangle.Empty) { Text = "Invite Friends", NormalColor = Color.DarkGreen, HoverColor = Color.Green };
        closeLobbyButton = new Button(uiTex, Rectangle.Empty) { Text = "Close Lobby (Play Solo)", NormalColor = Color.Firebrick, HoverColor = Color.IndianRed };
        exitButton = new Button(uiTex, Rectangle.Empty) { Text = "Quit to Main Menu", NormalColor = Color.DarkRed, HoverColor = Color.Red };

        NetworkRouter.OnPauseStateChanged += HandlePauseState;

        continueButton.OnClick += () => { TransmitPauseState(false); };

        inviteButton.OnClick += async () =>
        {
            var lobby = SteamManager.CurrentLobby;
            if (this.isHost || !lobby.HasValue)
            {
                inviteButton.IsEnabled = false;
                if (!lobby.HasValue)
                {
                    await SteamManager.CreateLobby();
                    SteamManager.CurrentLobby?.SetData("GameState", "InGame");
                }

                if (SteamManager.CurrentLobby.HasValue) friendsOverlay.Show();
                inviteButton.IsEnabled = true;
            }
        };

        closeLobbyButton.OnClick += () =>
        {
            SteamManager.LeaveLobby();
            TransmitPauseState(false);
        };

        exitButton.OnClick += () =>
        {
            SteamManager.LeaveLobby();
            stateManager.ChangeState(new MainMenuState(game, stateManager));
        };
    }

    public void Unload() => NetworkRouter.OnPauseStateChanged -= HandlePauseState;

    private void HandlePauseState(bool state) => IsPaused = state;

    public void Update()
    {
        UpdateNetworkStatusText();
        friendsOverlay.Update();
        if (friendsOverlay.IsVisible) return;

        var lobby = SteamManager.CurrentLobby;

        // ARCHITECTURE FIX: Pattern matching guarantees null-safety for the compiler
        if (lobby is { } activeLobby)
        {
            int currentMembers = activeLobby.MemberCount;
            if (IsPaused && currentMembers < previousMemberCount) TransmitPauseState(false);
            if (IsPaused && currentMembers > previousMemberCount && isHost) TransmitPauseState(true);
            previousMemberCount = currentMembers;
        }

        if (InputManager.ConsumeAction(GameActions.Pause)) TransmitPauseState(!IsPaused);

        if (IsPaused)
        {
            var viewport = game.GraphicsDevice.Viewport;
            int centerX = (viewport.Width / 2) - 150;
            int startY = (viewport.Height / 2) - 100;
            int offset = 0;

            continueButton.Bounds = new Rectangle(centerX, startY + offset, 300, 45); offset += 60;

            bool isHostOrSolo = !lobby.HasValue || isHost;
            if (isHostOrSolo)
            {
                inviteButton.Bounds = new Rectangle(centerX, startY + offset, 300, 45); offset += 60;
            }

            if (lobby.HasValue)
            {
                closeLobbyButton.Text = isHost ? "Close Lobby (Play Solo)" : "Leave Lobby (Play Solo)";
                closeLobbyButton.Bounds = new Rectangle(centerX, startY + offset, 300, 45); offset += 60;
            }

            exitButton.Bounds = new Rectangle(centerX, startY + offset, 300, 45);

            Point mousePos = InputManager.GetMousePosition();
            bool isClicked = InputManager.ConsumeUIClick();

            continueButton.Update(mousePos, isClicked);
            if (isHostOrSolo) inviteButton.Update(mousePos, isClicked);
            if (lobby.HasValue) closeLobbyButton.Update(mousePos, isClicked);
            exitButton.Update(mousePos, isClicked);
        }
    }

    private void UpdateNetworkStatusText()
    {
        var lobby = SteamManager.CurrentLobby;
        if (!SteamManager.IsSteamActive) networkStatusText = "Network Offline (Local Solo)";
        else if (lobby is { } activeLobby) networkStatusText = isHost ? $"Hosting Match: {activeLobby.MemberCount} Players" : "Connected to Host";
        else networkStatusText = "Playing Solo (Offline)";
    }

    private void TransmitPauseState(bool enforcePause)
    {
        IsPaused = enforcePause;
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
        if (IsPaused)
        {
            var viewport = game.GraphicsDevice.Viewport;
            spriteBatch.Draw(AssetManager.WhitePixel, viewport.Bounds, Color.Black * 0.85f);

            continueButton.Draw(spriteBatch);

            bool isHostOrSolo = !SteamManager.CurrentLobby.HasValue || isHost;
            if (isHostOrSolo) inviteButton.Draw(spriteBatch);

            if (SteamManager.CurrentLobby.HasValue) closeLobbyButton.Draw(spriteBatch);
            exitButton.Draw(spriteBatch);

            if (AssetManager.IsFontLoaded)
            {
                SpriteFontBase font = AssetManager.GetFont(24f);
                var textSize = font.MeasureString(networkStatusText);
                System.Numerics.Vector2 textPos = new System.Numerics.Vector2((viewport.Width - textSize.X) / 2f, (viewport.Height / 2f) - 170);
                font.DrawText(AssetManager.FontRenderer, networkStatusText, textPos, new FSColor(135, 206, 250, 255));
            }

            friendsOverlay.Draw(spriteBatch);
        }
    }
}
