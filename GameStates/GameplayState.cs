using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.Networking;
using MyGame.Gameplay.Components;
using MyGame.Gameplay.Prefabs;
using MyGame.GameStates.UI;
using Flecs.NET.Core;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.GameStates;

public class GameplayState : GameState
{
    private readonly World ecsWorld;
    private readonly int selectedClassId;

    private Texture2D dummyPlayerTexture = null!;
    private PauseMenuOverlay pauseMenu = null!;

    public GameplayState(Game1 game, StateManager stateManager, World sharedWorld, int chosenClassId)
        : base(game, stateManager)
    {
        ecsWorld = sharedWorld;
        selectedClassId = chosenClassId;
    }

    public override void LoadContent()
    {
        dummyPlayerTexture = new Texture2D(game.GraphicsDevice, 32, 32);
        XnaColor[] colorData = new XnaColor[32 * 32];
        Array.Fill(colorData, XnaColor.White);
        dummyPlayerTexture.SetData(colorData);

        pauseMenu = new PauseMenuOverlay(game, stateManager);

        // Ensures the ID sequence is clean for a new match
        NetworkIdGenerator.ResetSequence();

        PlayerFactory.CreateLocal(ecsWorld, selectedClassId);
    }

    public override void UnloadContent()
    {
        dummyPlayerTexture.Dispose();
        pauseMenu.Unload();

        ((Game1)game).SessionManager.ClearSessions();

        var garbageCollectionList = new List<Entity>();

        using var cleanupQuery = ecsWorld.QueryBuilder().With<MatchEntityTag>().Build();
        cleanupQuery.Each((Entity e) =>
        {
            garbageCollectionList.Add(e);
        });

        foreach (var entity in garbageCollectionList)
        {
            // Prevents Access Violations when destroying child entities that were automatically
            // purged by Flecs when their parent was destroyed earlier in this exact loop.
            if (entity.IsAlive())
            {
                entity.Destruct();
            }
        }

        Console.WriteLine("[Gameplay]: Active match simulation cleared safely via Deferred Teardown.");
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

        if (!pauseMenu.IsPaused)
        {
            float dt = (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.1);
            ecsWorld.Progress(dt);
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        game.GraphicsDevice.Clear(XnaColor.FromNonPremultiplied(30, 30, 30, 255));

        ecsWorld.Query<Position, CharacterClass>().Each((Iter it, int row, ref Position pos, ref CharacterClass cClass) =>
        {
            Entity e = it.Entity(row);
            XnaColor renderColor = cClass.Id == 0 ? XnaColor.Orange : XnaColor.Cyan;

            if (e.Has<RemotePlayerTag>()) renderColor = XnaColor.LightSkyBlue;

            spriteBatch.Draw(dummyPlayerTexture, new Rectangle((int)pos.X, (int)pos.Y, 32, 32), renderColor);
        });

        pauseMenu.Draw(spriteBatch);
    }
}
