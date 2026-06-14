using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.GameStates;
using MyGame.Engine.Networking;
using MyGame.Gameplay.Systems;
using MyGame.Gameplay.Networking;
using Flecs.NET.Core;

namespace MyGame;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch? spriteBatch;
    private readonly StateManager stateManager;

    public static Game1 Instance { get; private set; } = null!;

    public World EcsWorld { get; private set; }
    public RemoteSessionManager SessionManager { get; private set; }

    public Game1()
    {
        Instance = this;

        EcsWorld = World.Create();
        SessionManager = new RemoteSessionManager();

        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / 60d);

        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        stateManager = new StateManager();
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);

        LocalPlayerSystems.Register(EcsWorld);
        RemotePlayerSystems.Register(EcsWorld);

        NetworkReceiverSystem.Register(EcsWorld, SessionManager);
        NetworkBroadcastSystem.Register(EcsWorld);
        NetworkCleanupSystem.Register(EcsWorld, SessionManager);

        stateManager.ChangeState(new MainMenuState(this, stateManager));
    }

    protected override void Update(GameTime gameTime)
    {
        SteamManager.Update();
        stateManager.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        spriteBatch?.Begin();
        stateManager.Draw(spriteBatch!);
        spriteBatch?.End();

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            spriteBatch?.Dispose();
            SteamManager.Shutdown();
            EcsWorld.Dispose();
        }
        base.Dispose(disposing);
    }
}
