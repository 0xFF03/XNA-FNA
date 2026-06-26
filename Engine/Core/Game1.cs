using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.Networking;
#if DEBUG
using MyGame.Engine.Platform.Debug;
#endif
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Engine.StandardModules.Rendering2D;
using MyGame.Engine.StandardModules.WorldGen;
using MyGame.Game.UIStates;
using MyGame.Game.Core;
using MyGame.Game.Logic;

using FlecsWorld = Flecs.NET.Core.World;

namespace MyGame.Engine.Core;

public class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch? spriteBatch;
    private readonly StateManager stateManager;

    private const float LogicTickRate = 1f / 60f;
    private double _timeAccumulator = 0.0;

    public static Game1 Instance { get; private set; } = null!;
    public static double LastUpdateDurationMs { get; private set; }

    public FlecsWorld EcsWorld { get; private set; }

#if DEBUG
    public DebugUIManager DebugUI { get; private set; } = null!;
#endif

    public Game1()
    {
        Instance = this;
        EcsWorld = FlecsWorld.Create();

        graphics = new GraphicsDeviceManager(this)
        {
           PreferredBackBufferWidth = 1280,
           PreferredBackBufferHeight = 720
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        stateManager = new StateManager();
    }

    protected override void Initialize()
    {
        graphics.ApplyChanges();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        EngineLogger.Log("Phase 1: Booting Hardware & Rendering...", "SYSTEM");
        spriteBatch = new SpriteBatch(GraphicsDevice);
        AssetManager.Initialize(GraphicsDevice, spriteBatch);

        try { AssetManager.LoadFont("Fonts/DefaultFont.ttf"); }
        catch (System.Exception ex) { EngineLogger.LogError("Font Load Failed", ex); }

#if DEBUG
        DebugUI = new DebugUIManager();
        DebugUI.Initialize(this);
#endif

        EngineLogger.Log("Phase 2: Initializing Network Service...", "SYSTEM");
        NetworkServiceLocator.Initialize();

        EngineLogger.Log("Phase 3: Database Bypassed [RAM ONLY MODE]", "SYSTEM");

        EngineLogger.Log("Phase 4: Loading Game Configuration...", "SYSTEM");
        SettingsManager.Initialize(this, graphics);
        SaveManager.Initialize(EcsWorld);

        EngineLogger.Log("Phase 5: Booting ECS Game Domain...", "SYSTEM");
        TransformSystems.Register(EcsWorld);
        MovementControllers.Register(EcsWorld);
        KinematicInterpolator.Register(EcsWorld);

        AltitudeSystem.Register(EcsWorld);
        DimensionTransferSystem.Register(EcsWorld);
        InteractionSystem.Register(EcsWorld);
        PhysicsWorldManager.Register(EcsWorld);

        NetworkRegistry.Register(EcsWorld);
        NetworkReceiverSystem.Register(EcsWorld);
        NetworkBroadcastSystem.Register(EcsWorld);
        NetworkCleanupSystem.Register(EcsWorld);

        ProjectileSystem.Register(EcsWorld);
        ShooterHitDetection.Register(EcsWorld);
        DistributedEventSystem.Register(EcsWorld);

        ProceduralGenerationSystem.Register(EcsWorld);
        SessionProgressionSystem.Register(EcsWorld);
        LevelManager.Initialize(EcsWorld);

        TileMapRenderer.Initialize(EcsWorld, spriteBatch);

        stateManager.ChangeState(new MainMenuState(this, stateManager));
        EngineLogger.Log("Engine Boot Sequence Complete.", "SYSTEM");
    }

    protected override void Update(GameTime gameTime)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        NetworkServiceLocator.Update();
        NetworkRouter.RouteControlPackets();

        InputManager.Update();

        _timeAccumulator += gameTime.ElapsedGameTime.TotalSeconds;
        if (_timeAccumulator > LogicTickRate * 10) _timeAccumulator = LogicTickRate * 10;

        bool logicExecuted = false;

        while (_timeAccumulator >= LogicTickRate)
        {
            stateManager.Update((float)LogicTickRate);
            _timeAccumulator -= LogicTickRate;
            logicExecuted = true;
        }

        if (logicExecuted)
        {
            InputManager.PostLogicUpdate();
        }

        base.Update(gameTime);
        sw.Stop();

        LastUpdateDurationMs = sw.Elapsed.TotalMilliseconds;

        if (LastUpdateDurationMs > 18.0)
        {
            EngineLogger.LogPerformance("Game1.Update", LastUpdateDurationMs);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        float alpha = (float)(_timeAccumulator / LogicTickRate);

        if (spriteBatch != null && !stateManager.IsTransitioning)
        {
            stateManager.Draw(spriteBatch, alpha);
#if DEBUG
            DebugUI.Draw(gameTime);
#endif
        }

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            spriteBatch?.Dispose();
            NetworkServiceLocator.Shutdown();
            EcsWorld.Dispose();
            AssetManager.UnloadAll();
            PhysicsWorldManager.ClearAll();
            EngineLogger.Shutdown();
        }
        base.Dispose(disposing);
    }
}
