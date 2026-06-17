using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.UI;
using MyGame.Engine.Core;
using MyGame.Engine.Networking;
using MyGame.Engine.Input;
using Steamworks;

namespace MyGame.Game.UIStates;

public class CharacterSelectState : GameState
{
    private Button startRunButton = null!;
    private Button multiplayerButton = null!;
    private Button closeLobbyButton = null!;
    private Button backButton = null!;

    private FriendsListOverlay friendsOverlay = null!;

    private const int DefaultClassId = 0;
    private bool isJoineeReady = false;
    private int previousMemberCount = 0;

    private const string TargetMapPath = "Maps/GameWorld.ldtk";

    public CharacterSelectState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

    public override void LoadContent()
    {
        Texture2D uiTex = AssetManager.WhitePixel;
        friendsOverlay = new FriendsListOverlay(game);

        startRunButton = new Button(uiTex, Rectangle.Empty);
        multiplayerButton = new Button(uiTex, Rectangle.Empty);
        closeLobbyButton = new Button(uiTex, Rectangle.Empty) { Text = "Close Lobby", NormalColor = Color.Firebrick, HoverColor = Color.IndianRed };
        backButton = new Button(uiTex, Rectangle.Empty) { Text = "Back to Menu", NormalColor = Color.DarkRed, HoverColor = Color.Red };

        NetworkRouter.OnLobbyMatchStart += HandleLobbyMatchStart;
        NetworkRouter.OnJoineeReady += HandleJoineeReady;

        startRunButton.OnClick += () =>
        {
            var lobby = SteamManager.CurrentLobby;
            bool isHost = !lobby.HasValue || lobby.Value.Owner.Id == SteamClient.SteamId;

            if (isHost)
            {
                // ARCHITECTURE FIX: Safe unwrap with pattern matching
                if (lobby is { } activeLobby)
                {
                    byte[] signalBuffer = new byte[] { PacketTypes.LobbyStart };
                    foreach (var member in activeLobby.Members)
                    {
                        if (member.Id != SteamClient.SteamId)
                            SteamNetworking.SendP2PPacket(member.Id, signalBuffer, signalBuffer.Length, 2, P2PSend.Reliable);
                    }
                }
                stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld, DefaultClassId, TargetMapPath));
            }
            else
            {
                bool isMatchInProgress = lobby is { } l && l.GetData("GameState") == "InGame";
                if (isMatchInProgress)
                {
                    stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld, DefaultClassId, TargetMapPath));
                }
                // ARCHITECTURE FIX: Prevent static property race-condition warnings
                else if (SteamManager.KnownHostId is { } hostId)
                {
                    isJoineeReady = !isJoineeReady;
                    byte[] readyBuffer = new byte[] { PacketTypes.PlayerReady };
                    SteamNetworking.SendP2PPacket(hostId, readyBuffer, readyBuffer.Length, 2, P2PSend.Reliable);
                }
            }
        };

        multiplayerButton.OnClick += async () =>
        {
            if (!SteamManager.CurrentLobby.HasValue)
            {
                multiplayerButton.IsEnabled = false;
                multiplayerButton.Text = "Creating Lobby...";
                await SteamManager.CreateLobby();
            }
            else
            {
                friendsOverlay.Show();
            }
        };

        closeLobbyButton.OnClick += () =>
        {
            if (SteamManager.CurrentLobby.HasValue)
            {
                SteamManager.LeaveLobby();
                isJoineeReady = false;
            }
        };

        backButton.OnClick += () =>
        {
            if (SteamManager.CurrentLobby.HasValue) SteamManager.LeaveLobby();
            stateManager.ChangeState(new MainMenuState(game, stateManager));
        };
    }

    public override void UnloadContent()
    {
        NetworkRouter.OnLobbyMatchStart -= HandleLobbyMatchStart;
        NetworkRouter.OnJoineeReady -= HandleJoineeReady;
    }

    private void HandleLobbyMatchStart() => stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld, DefaultClassId, TargetMapPath));
    private void HandleJoineeReady() => isJoineeReady = true;

    public override void Update(GameTime gameTime)
    {
        friendsOverlay.Update();
        if (friendsOverlay.IsVisible) return;

        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 125;
        int startY = (viewport.Height / 2) - 100;

        var lobby = SteamManager.CurrentLobby;
        bool inLobby = lobby.HasValue;

        // Clean defaulting values
        bool isHost = true;
        bool isMatchInProgress = false;
        int currentMembers = 1;

        // ARCHITECTURE FIX: Safe extraction of all struct data
        if (lobby is { } activeLobby)
        {
            isHost = activeLobby.Owner.Id == SteamClient.SteamId;
            isMatchInProgress = activeLobby.GetData("GameState") == "InGame";
            currentMembers = activeLobby.MemberCount;
        }

        if (currentMembers < previousMemberCount) isJoineeReady = false;
        previousMemberCount = currentMembers;

        ConfigureUIStates(inLobby, isHost, currentMembers, isMatchInProgress);

        int buttonOffset = 0;
        startRunButton.Bounds = new Rectangle(centerX, startY + buttonOffset, 250, 45); buttonOffset += 60;
        multiplayerButton.Bounds = new Rectangle(centerX, startY + buttonOffset, 250, 45); buttonOffset += 60;

        if (inLobby)
        {
            closeLobbyButton.Bounds = new Rectangle(centerX, startY + buttonOffset, 250, 45);
            buttonOffset += 60;
        }

        backButton.Bounds = new Rectangle(centerX, startY + buttonOffset, 250, 45);

        Point mousePos = InputManager.GetMousePosition();
        bool isClicked = InputManager.ConsumeUIClick();

        startRunButton.Update(mousePos, isClicked);
        multiplayerButton.Update(mousePos, isClicked);
        if (inLobby) closeLobbyButton.Update(mousePos, isClicked);
        backButton.Update(mousePos, isClicked);
    }

    private void ConfigureUIStates(bool inLobby, bool isHost, int currentMembers, bool isMatchInProgress)
    {
        if (!inLobby)
        {
            if (multiplayerButton.Text != "Creating Lobby...")
            {
                multiplayerButton.Text = "Host Multiplayer";
                multiplayerButton.IsEnabled = true;
            }
            multiplayerButton.NormalColor = Color.DarkGoldenrod;
            multiplayerButton.HoverColor = Color.Goldenrod;

            startRunButton.Text = "Start Solo Run";
            startRunButton.NormalColor = Color.DarkGreen;
            startRunButton.HoverColor = Color.Green;
            startRunButton.IsEnabled = true;
        }
        else if (isHost)
        {
            multiplayerButton.Text = "Invite Friends";
            multiplayerButton.NormalColor = Color.DarkGreen;
            multiplayerButton.HoverColor = Color.Green;
            multiplayerButton.IsEnabled = true;
            closeLobbyButton.Text = "Close Lobby";

            if (currentMembers == 1)
            {
                startRunButton.Text = "Start Match (Solo)";
                startRunButton.NormalColor = Color.DarkGreen;
                startRunButton.HoverColor = Color.Green;
                startRunButton.IsEnabled = true;
            }
            else
            {
                startRunButton.Text = isJoineeReady ? "Start Match" : "Waiting for Joinee...";
                startRunButton.NormalColor = isJoineeReady ? Color.DarkGreen : Color.DarkSlateGray;
                startRunButton.HoverColor = isJoineeReady ? Color.Green : Color.DarkSlateGray;
                startRunButton.IsEnabled = isJoineeReady;
            }
        }
        else
        {
            multiplayerButton.Text = "Connected to Host";
            multiplayerButton.NormalColor = Color.DarkSlateGray;
            multiplayerButton.HoverColor = Color.DarkSlateGray;
            multiplayerButton.IsEnabled = false;
            closeLobbyButton.Text = "Leave Lobby";

            if (isMatchInProgress)
            {
                startRunButton.Text = "Join Ongoing Match";
                startRunButton.NormalColor = Color.DarkGoldenrod;
                startRunButton.HoverColor = Color.Goldenrod;
                startRunButton.IsEnabled = true;
            }
            else
            {
                startRunButton.Text = isJoineeReady ? "Waiting for Host..." : "Ready Up";
                startRunButton.NormalColor = isJoineeReady ? Color.DarkSlateGray : Color.DarkSlateBlue;
                startRunButton.HoverColor = isJoineeReady ? Color.DarkSlateGray : Color.SlateBlue;
                startRunButton.IsEnabled = !isJoineeReady;
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        game.GraphicsDevice.Clear(Color.DarkSlateGray);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

        startRunButton.Draw(spriteBatch);
        multiplayerButton.Draw(spriteBatch);
        if (SteamManager.CurrentLobby.HasValue) closeLobbyButton.Draw(spriteBatch);
        backButton.Draw(spriteBatch);

        friendsOverlay.Draw(spriteBatch);
        spriteBatch.End();
    }
}
