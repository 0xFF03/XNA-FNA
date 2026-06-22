using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.Debug;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Engine.StandardModules.Rendering2D;
using MyGame.Game.UIStates;
using MyGame.Game.Core;
using MyGame.Game.Logic;
using LiteDB;

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
    public LiteDatabase LocalDatabase { get; private set; }
    public DebugUIManager DebugUI { get; private set; } = null!;

    public Game1()
    {
        Instance = this;
        EcsWorld = FlecsWorld.Create();

        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyGame", "SaveData.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        LocalDatabase = new LiteDatabase(dbPath);

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

    protected override void OnActivated(object sender, EventArgs args)
    {
        InputManager.ResetState();
        base.OnActivated(sender, args);
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        AssetManager.Initialize(GraphicsDevice, spriteBatch);
        SettingsManager.Initialize(this, graphics);

        SaveManager.Initialize();

        try { AssetManager.LoadFont("Fonts/DefaultFont.ttf"); }
        catch (System.Exception ex) { EngineLogger.LogError("Font Load Failed", ex); }

        DebugUI = new DebugUIManager();
        DebugUI.Initialize(this);

        TransformSystems.Register(EcsWorld);
        MovementControllers.Register(EcsWorld);
        KinematicInterpolator.Register(EcsWorld);

        AltitudeSystem.Register(EcsWorld);
        DimensionTransferSystem.Register(EcsWorld);
        InteractionSystem.Register(EcsWorld);

        NetworkRegistry.Register(EcsWorld);
        NetworkReceiverSystem.Register(EcsWorld);
        NetworkBroadcastSystem.Register(EcsWorld);
        NetworkCleanupSystem.Register(EcsWorld);

        ProjectileSystem.Register(EcsWorld);
        ShooterHitDetection.Register(EcsWorld);
        DistributedEventSystem.Register(EcsWorld);

        TileMapRenderer.Initialize(EcsWorld, spriteBatch);
        PlaceholderPlayerRenderer.Initialize(EcsWorld);
        PlaceholderProjectileRenderer.Initialize(EcsWorld);

        stateManager.ChangeState(new MainMenuState(this, stateManager));
    }

    protected override void Update(GameTime gameTime)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        SteamManager.Update();
        NetworkRouter.RouteControlPackets();

        // Gathers inputs at unlimited hardware framerate
        InputManager.Update();

        _timeAccumulator += gameTime.ElapsedGameTime.TotalSeconds;
        if (_timeAccumulator > LogicTickRate * 10) _timeAccumulator = LogicTickRate * 10;

        while (_timeAccumulator >= LogicTickRate)
        {
           stateManager.Update((float)LogicTickRate);

           // ARCHITECTURE FIX: Securely flushes inputs ONLY after logic has processed them
           InputManager.PostLogicUpdate();

           _timeAccumulator -= LogicTickRate;
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
            DebugUI.Draw(gameTime);
        }

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            spriteBatch?.Dispose();
            SteamAvatarCache.Clear();
            SteamManager.Shutdown();
            EcsWorld.Dispose();
            LocalDatabase.Dispose();
            AssetManager.UnloadAll();
            PhysicsWorldManager.ClearAll();
            EngineLogger.Shutdown();
        }
        base.Dispose(disposing);
    }
}
