using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.Networking;
using Steamworks;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.GameStates;

public class LobbyState : GameState
{
    private Texture2D? uiTexture;
    private readonly byte[] signalBuffer = new byte[1];
    private readonly int selectedClassId;

    public LobbyState(Game1 game, StateManager stateManager, int chosenClassId) : base(game, stateManager)
    {
        selectedClassId = chosenClassId;
    }

    public override void LoadContent()
    {
        uiTexture = new Texture2D(game.GraphicsDevice, 1, 1);
        uiTexture.SetData(new[] { XnaColor.White });
    }

    public override void UnloadContent()
    {
        uiTexture?.Dispose();
    }

    public override void Update(GameTime gameTime)
    {
        if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue)
        {
            stateManager.ChangeState(new MainMenuState(game, stateManager));
            return;
        }

        ListenForLobbySignals();
    }

    private void ListenForLobbySignals()
    {
        while (SteamNetworking.IsP2PPacketAvailable(1))
        {
            var packetData = SteamNetworking.ReadP2PPacket(1);
            if (packetData.HasValue && packetData.Value.Data.Length > 0)
            {
                byte signalType = packetData.Value.Data[0];

                if (signalType == 99)
                {
                    Console.WriteLine("[Lobby Sync]: Start signal received. Locking doors and launching...");
                    stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld, selectedClassId));
                }
            }
        }
    }

    private void HandleStartGamePressed()
    {
        if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

        Console.WriteLine("[Lobby Sync]: Host initiated match launch. Broadcasting signals to peers...");

        var lobby = SteamManager.CurrentLobby.Value;
        lobby.SetJoinable(false);

        signalBuffer[0] = 99;
        foreach (var member in lobby.Members)
        {
            if (member.Id == SteamClient.SteamId) continue;
            SteamNetworking.SendP2PPacket(member.Id, signalBuffer, signalBuffer.Length, 1, P2PSend.Reliable);
        }

        stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld, selectedClassId));
    }

    // Example of calling handle start internally
    private void CheckHostUIInteraction(Rectangle buttonBounds, Point mousePoint)
    {
        if (buttonBounds.Contains(mousePoint)) HandleStartGamePressed();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        game.GraphicsDevice.Clear(XnaColor.FromNonPremultiplied(20, 24, 32, 255));
        if (uiTexture == null || !SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

        var lobby = SteamManager.CurrentLobby.Value;
        bool isHost = lobby.Owner.Id == SteamClient.SteamId;

        DrawButton(spriteBatch, new Rectangle(50, 50, 160, 40), "Leave Lobby", XnaColor.Crimson);

        if (isHost) DrawButton(spriteBatch, new Rectangle(50, 110, 160, 40), "Start Run", XnaColor.ForestGreen);
        else DrawButton(spriteBatch, new Rectangle(50, 110, 240, 40), "Waiting for Host...", XnaColor.DarkSlateGray);
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string text, XnaColor color)
    {
        if (uiTexture != null) spriteBatch.Draw(uiTexture, rect, color);
    }
}
