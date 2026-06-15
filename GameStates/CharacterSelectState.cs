using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.UI;
using MyGame.Engine.Core;
using MyGame.Engine.Networking;
using Steamworks;

namespace MyGame.GameStates;

public class CharacterSelectState : GameState
{
    private Button startRunButton = null!;
    private Button inviteButton = null!;
    private Button backButton = null!;

    private const int DefaultClassId = 0;

    public CharacterSelectState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

    public override void LoadContent()
    {
        Texture2D uiTex = AssetManager.WhitePixel;

        startRunButton = new Button(uiTex, Rectangle.Empty);
        inviteButton = new Button(uiTex, Rectangle.Empty)
            { Text = "Invite Friends", NormalColor = Color.DarkGreen, HoverColor = Color.Green };
        backButton = new Button(uiTex, Rectangle.Empty)
            { Text = "Back", NormalColor = Color.DarkRed, HoverColor = Color.Red };

        startRunButton.OnClick += () =>
        {
            if (SteamManager.CurrentLobby.HasValue && SteamManager.CurrentLobby.Value.Owner.Id == SteamClient.SteamId)
            {
                SteamManager.CurrentLobby.Value.SetJoinable(false);

                byte[] signalBuffer = new byte[] { PacketTypes.LobbyStart };
                foreach (var member in SteamManager.CurrentLobby.Value.Members)
                {
                    if (member.Id != SteamClient.SteamId)
                    {
                        SteamNetworking.SendP2PPacket(member.Id, signalBuffer, signalBuffer.Length, 2, P2PSend.Reliable);
                    }
                }
                stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld, DefaultClassId));
            }
        };

        inviteButton.OnClick += () => SteamManager.OpenInviteOverlay();
        backButton.OnClick += () =>
        {
            SteamManager.LeaveLobby();
            stateManager.ChangeState(new MainMenuState(game, stateManager));
        };
    }

    public override void Update(GameTime gameTime)
    {
        ListenForLobbySignals();

        if (!SteamManager.CurrentLobby.HasValue)
        {
            stateManager.ChangeState(new MainMenuState(game, stateManager));
            return;
        }

        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 100;
        int startY = (viewport.Height / 2) - 80;

        startRunButton.Bounds = new Rectangle(centerX, startY, 200, 50);
        inviteButton.Bounds = new Rectangle(centerX, startY + 70, 200, 50);
        backButton.Bounds = new Rectangle(centerX, startY + 140, 200, 50);

        bool isHost = SteamManager.CurrentLobby.Value.Owner.Id == SteamClient.SteamId;

        if (isHost)
        {
            startRunButton.Text = "Start Run";
            startRunButton.NormalColor = Color.DarkGreen;
            startRunButton.HoverColor = Color.Green;
            startRunButton.Update();
        }
        else
        {
            startRunButton.Text = "Waiting for Host...";
            startRunButton.NormalColor = Color.DarkSlateGray;
            startRunButton.HoverColor = Color.DarkSlateGray;
        }

        inviteButton.Update();
        backButton.Update();
    }

    private void ListenForLobbySignals()
    {
        if (!SteamManager.IsSteamActive) return;

        while (SteamNetworking.IsP2PPacketAvailable(2))
        {
            var packetData = SteamNetworking.ReadP2PPacket(2);
            if (packetData.HasValue && packetData.Value.Data.Length > 0)
            {
                byte signal = packetData.Value.Data[0];
                if (signal == PacketTypes.LobbyStart)
                {
                    stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld, DefaultClassId));
                }
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        game.GraphicsDevice.Clear(Color.DarkSlateGray);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

        startRunButton.Draw(spriteBatch);
        inviteButton.Draw(spriteBatch);
        backButton.Draw(spriteBatch);

        spriteBatch.End();
    }
}
