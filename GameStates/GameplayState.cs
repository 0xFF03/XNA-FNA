using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Gameplay.Components;
using MyGame.Gameplay.Systems;
using MyGame.Gameplay.Networking;
using MyGame.Gameplay.Prefabs;
using Flecs.NET.Core;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.GameStates;

public class GameplayState : GameState
{
    private readonly World _ecsWorld;
    private readonly RemoteSessionManager _sessionManager;
    private Texture2D _dummyPlayerTexture = null!;
    private readonly int _selectedClassId;

    public GameplayState(Game1 game, StateManager stateManager) : base(game, stateManager)
    {
        _ecsWorld = World.Create();
        _sessionManager = new RemoteSessionManager();
    }

    public override void LoadContent()
    {
        // Register your fully modular logic pipelines
        MovementSystems.Register(_ecsWorld);
        NetworkReceiverSystem.Register(_ecsWorld, _sessionManager);
        NetworkBroadcastSystem.Register(_ecsWorld);

        _dummyPlayerTexture = new Texture2D(game.GraphicsDevice, 32, 32);
        XnaColor[] colorData = new XnaColor[32 * 32];
        Array.Fill(colorData, XnaColor.White);
        _dummyPlayerTexture.SetData(colorData);

        PlayerFactory.CreateLocal(_ecsWorld, _selectedClassId);

        Console.WriteLine("[Gameplay]: Flecs simulation initialized under Client-Side Authority.");
    }

    public override void UnloadContent()
    {
        _dummyPlayerTexture.Dispose();
        _sessionManager.ClearSessions();
        _ecsWorld.Dispose();
        Console.WriteLine("[Gameplay]: Native Flecs ECS World safely dismantled.");
    }

    public override void Update(GameTime gameTime)
    {
        // The magic of pure ECS: This single line now runs Physics, Ingests Packets, AND Broadcasts Packets at exact intervals!
        _ecsWorld.Progress((float)gameTime.ElapsedGameTime.TotalSeconds);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        game.GraphicsDevice.Clear(XnaColor.FromNonPremultiplied(30, 30, 30, 255));

        _ecsWorld.Query<Position, CharacterClass>().Each((Iter it, int row, ref Position pos, ref CharacterClass cClass) =>
        {
            Entity e = it.Entity(row);
            XnaColor renderColor = cClass.Id == 0 ? XnaColor.Orange : XnaColor.Cyan;

            if (e.Has<RemotePlayerTag>())
                renderColor = XnaColor.LightSkyBlue;

            spriteBatch.Draw(_dummyPlayerTexture, new Rectangle((int)pos.X, (int)pos.Y, 32, 32), renderColor);
        });
    }
}
