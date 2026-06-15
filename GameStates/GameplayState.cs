using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.Networking;
using MyGame.Engine.Rendering;
using MyGame.Engine.Core;
using MyGame.Gameplay.Components;
using MyGame.Gameplay.Prefabs;
using MyGame.GameStates.UI;
using MyGame.Gameplay.Systems;
using Flecs.NET.Core;
using Steamworks;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.GameStates;

public class GameplayState : GameState
{
    private readonly Flecs.NET.Core.World ecsWorld;
    private readonly int selectedClassId;
    private PauseMenuOverlay pauseMenu = null!;
    private Camera2D camera = null!;

    public bool IsSimulationPaused => pauseMenu != null && pauseMenu.IsPaused;

    public GameplayState(Game1 game, StateManager stateManager, Flecs.NET.Core.World sharedWorld, int chosenClassId)
        : base(game, stateManager)
    {
        ecsWorld = sharedWorld;
        selectedClassId = chosenClassId;
    }

    public override void LoadContent()
    {
        pauseMenu = new PauseMenuOverlay(game, stateManager);
        camera = new Camera2D(game.GraphicsDevice);
        camera.Position = new Vector2(400, 300);

        Entity localAvatar = PlayerFactory.CreateLocal(ecsWorld, selectedClassId);

        if (SteamManager.IsSteamActive && SteamManager.CurrentLobby.HasValue)
        {
            var handshakePayload = new PlayerSpawnPacket
            {
                PacketType = PacketTypes.Spawn,
                CharacterClassId = selectedClassId,
                StartX = 400f,
                StartY = 300f,
                EntityNetworkSequenceId = localAvatar.Get<NetworkId>().Value
            };

            int bufferSize = System.Runtime.InteropServices.Marshal.SizeOf<PlayerSpawnPacket>();
            byte[] handshakeBuffer = new byte[bufferSize];
            handshakePayload.SerializeTo(handshakeBuffer);

            foreach (var peer in SteamManager.CurrentLobby.Value.Members)
            {
                if (peer.Id == SteamClient.SteamId) continue;

                SteamNetworking.SendP2PPacket(peer.Id, handshakeBuffer, handshakeBuffer.Length, 1, P2PSend.Reliable);
            }
            Console.WriteLine("[Network Handshake]: Reliable introduction frame broadcast to session lobby.");
        }
    }

    public override void UnloadContent()
    {
        pauseMenu.Unload();

        var garbageCollectionList = new List<Entity>();

        using var cleanupQuery = ecsWorld.QueryBuilder().With<MatchEntityTag>().Build();
        cleanupQuery.Each((Entity e) =>
        {
            garbageCollectionList.Add(e);
        });

        foreach (var entity in garbageCollectionList)
        {
            if (entity.IsAlive())
            {
                entity.Destruct();
            }
        }

        NetworkReceiverSystem.ClearShadows();
        NetworkIdGenerator.ResetSequence();

        Console.WriteLine("[Gameplay]: Active match simulation cleared safely via Native Deferred Teardown.");
    }

    public override void Update(GameTime gameTime)
    {
        if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue)
        {
           SteamManager.LeaveLobby();
           stateManager.ChangeState(new MainMenuState(game, stateManager));
           return;
        }

        pauseMenu.Update();

        if (!IsSimulationPaused)
        {
           game.PhysicsWorld.Step(1f / 60f);

           float dt = (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.1);
           ecsWorld.Progress(dt);

           ecsWorld.Query<Position>().Each((Iter it, int row, ref Position pos) =>
           {
              Entity e = it.Entity(row);
              if (e.Has<LocalPlayerTag>())
              {
                 camera.Position = Vector2.Lerp(camera.Position, new Vector2(pos.X, pos.Y), 5f * dt);
              }
           });
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        game.GraphicsDevice.Clear(XnaColor.FromNonPremultiplied(30, 30, 30, 255));

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            null, null, null,
            camera.GetViewMatrix()
        );

        for (int x = -2000; x < 4000; x += 100)
        {
            for (int y = -2000; y < 4000; y += 100)
            {
                spriteBatch.Draw(AssetManager.WhitePixel, new Rectangle(x, y, 4, 4), XnaColor.DimGray);
            }
        }

        ecsWorld.Query<Position, CharacterClass>().Each((Iter it, int row, ref Position pos, ref CharacterClass cClass) =>
        {
            Entity e = it.Entity(row);
            XnaColor renderColor = cClass.Id == 0 ? XnaColor.Orange : XnaColor.Cyan;

            if (e.Has<RemotePlayerTag>()) renderColor = XnaColor.LightSkyBlue;

            Rectangle agentDestRect = new Rectangle((int)pos.X - 16, (int)pos.Y - 16, 32, 32);
            spriteBatch.Draw(AssetManager.WhitePixel, agentDestRect, renderColor);
        });

        spriteBatch.End();

        spriteBatch.Begin();
        pauseMenu.Draw(spriteBatch);
        spriteBatch.End();
    }
}
