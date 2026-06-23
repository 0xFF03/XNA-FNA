using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Rendering2D;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.StandardModules.Combat;
using Flecs.NET.Core;
using Steamworks;
using MyGame.Game.Core;
using MyGame.Game.Logic;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.Game.UIStates;

public class GameplayState : GameState
{
    private readonly World ecsWorld;

    private PauseMenuOverlay pauseMenu = null!;
    private Camera2D camera = null!;
    private RenderTarget2D virtualRenderTarget = null!;

    public const int VirtualWidth = 480;
    public const int VirtualHeight = 270;
    private readonly bool isHostOrigin;

    private Query<Position, PreviousPosition, PhysicsDimension, BaseCombatComponents.Health> _localPlayerQuery;

    private static float _drawAlpha;
    private static Camera2D _drawCamera = null!;

    public bool IsSimulationPaused => pauseMenu != null && pauseMenu.IsPaused;

    public GameplayState(Game1 game, StateManager stateManager, World sharedWorld)
        : base(game, stateManager)
    {
        ecsWorld = sharedWorld;
        isHostOrigin = !SteamManager.KnownHostId.HasValue || SteamManager.KnownHostId.Value == SteamClient.SteamId;

        if (SaveManager.CurrentProfile == null)
            EngineLogger.Log("GameplayState initialized without an active save profile!", "FATAL");
    }

    public override void LoadContent()
    {
        virtualRenderTarget = new RenderTarget2D(game.GraphicsDevice, VirtualWidth, VirtualHeight, false, SurfaceFormat.Color, DepthFormat.None);
        pauseMenu = new PauseMenuOverlay(game, stateManager, isHostOrigin);

        _localPlayerQuery = ecsWorld.QueryBuilder<Position, PreviousPosition, PhysicsDimension, BaseCombatComponents.Health>().With<LocalPlayerTag>().Build();

        pauseMenu.OnSaveSlotRequested += HandleManualSave;
        pauseMenu.OnLoadSlotRequested += HandleLoadRequest;

        camera = new Camera2D();
        camera.Zoom = 1.5f;

        if (isHostOrigin && SteamManager.IsSteamActive && SteamManager.CurrentLobby.HasValue)
        {
            SteamManager.CurrentLobby.Value.SetData("GameState", "InGame");
            NetworkRouter.OnClientLoadedMap += HandleJoineeLoadedMap;
        }

        PlaceholderVehicleRenderer.Initialize(ecsWorld);
        PlaceholderPlayerRenderer.Initialize(ecsWorld);
        PlaceholderProjectileRenderer.Initialize(ecsWorld);

        LevelManager.LoadInitialWorld(ecsWorld, SaveManager.CurrentProfile!, isHostOrigin);
    }

    private void HandleManualSave(int slotId)
    {
        _localPlayerQuery.Each((Entity player, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
        {
            var profile = SaveManager.CurrentProfile!;
            SaveManager.SaveToSlot(slotId, $"Manual Save {slotId}", profile.CurrentMapPath, pos.X, pos.Y, hp.Current, dim.Name, 0f);
        });
    }

    private void HandleLoadRequest(int slotId)
    {
        SaveManager.LoadProfile(slotId);
        stateManager.ChangeState(new GameplayState(game, stateManager, ecsWorld));
    }

    private void HandleJoineeLoadedMap(SteamId joineeId)
    {
        WorldSyncSystem.SendWorldSnapshot(ecsWorld, joineeId);
    }

    public override void UnloadContent()
    {
        pauseMenu.OnSaveSlotRequested -= HandleManualSave;
        pauseMenu.OnLoadSlotRequested -= HandleLoadRequest;
        pauseMenu.Unload();
        NetworkRouter.OnClientLoadedMap -= HandleJoineeLoadedMap;

        using var cleanupQuery = ecsWorld.QueryBuilder().With<MatchEntityTag>().Build();
        cleanupQuery.Each((Entity e) => { if (e.IsAlive()) e.Destruct(); });

        var mapEntity = ecsWorld.Entity("GlobalMapData");
        if (mapEntity.Has<MapComponents.MapInstance>()) mapEntity.Remove<MapComponents.MapInstance>();

        virtualRenderTarget?.Dispose();
        LevelManager.UnloadAll();
        AssetManager.UnloadLevelAssets();

        PhysicsWorldManager.ClearAll();
        NetworkRegistry.ClearAll();
    }

    public override void Update(float deltaTime)
    {
        if (SaveManager.CurrentProfile == null)
        {
            SteamManager.LeaveLobby();
            stateManager.ChangeState(new MainMenuState(game, stateManager));
            return;
        }

        if (SteamManager.IsSteamActive && SteamManager.CurrentLobby.HasValue)
        {
            if (!isHostOrigin && (!SteamManager.KnownHostId.HasValue || SteamManager.KnownHostId.Value == 0))
            {
                SteamManager.LeaveLobby();
                stateManager.ChangeState(new MainMenuState(game, stateManager));
                return;
            }
        }

        pauseMenu.Update();

        if (!IsSimulationPaused)
        {
            ecsWorld.Progress(deltaTime);

            // ARCHITECTURE FIX: Flush and update active dimensions natively per logic tick
            PhysicsWorldManager.ActiveDimensions.Clear();

            _localPlayerQuery.Each((Entity player, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
            {
                PhysicsWorldManager.ActiveDimensions.Add(dim.Name);

                var profile = SaveManager.CurrentProfile!;
                if (profile.CurrentDimension != dim.Name)
                {
                    string leavingDimension = profile.CurrentDimension;
                    profile.CurrentDimension = dim.Name;

                    LevelManager.HotReloadVisualMap(ecsWorld, dim.Name, player, leavingDimension);
                }
            });
        }
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        if (SaveManager.CurrentProfile == null) return;

        _drawAlpha = alpha;
        _drawCamera = camera;
        string activeDimension = "MacroSpace";

        _localPlayerQuery.Each((Entity player, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
        {
            activeDimension = dim.Name;

            float targetX = pos.X;
            float targetY = pos.Y;
            float prevTargetX = prevPos.X;
            float prevTargetY = prevPos.Y;

            if (player.Has<HelmControl>())
            {
                var helm = player.Get<HelmControl>();
                if (helm.ControlledVehicle.Id != 0 && helm.ControlledVehicle.IsAlive() && helm.ControlledVehicle.Has<Position>())
                {
                    var vPos = helm.ControlledVehicle.Get<Position>();
                    var vPrev = helm.ControlledVehicle.Has<PreviousPosition>() ? helm.ControlledVehicle.Get<PreviousPosition>() : new PreviousPosition { X = vPos.X, Y = vPos.Y };
                    targetX = vPos.X;
                    targetY = vPos.Y;
                    prevTargetX = vPrev.X;
                    prevTargetY = vPrev.Y;
                }
            }

            float lerpX = MathHelper.Lerp(prevTargetX, targetX, _drawAlpha);
            float lerpY = MathHelper.Lerp(prevTargetY, targetY, _drawAlpha);
            _drawCamera.Position = new Vector2(lerpX, lerpY); // Removed integer cast for buttery smooth camera
        });

        game.GraphicsDevice.SetRenderTarget(virtualRenderTarget);
        game.GraphicsDevice.Clear(XnaColor.FromNonPremultiplied(40, 35, 50, 255));

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, camera.GetViewMatrix(VirtualWidth, VirtualHeight));

        TileMapRenderer.Draw(camera, VirtualWidth, VirtualHeight);
        PlaceholderVehicleRenderer.Draw(spriteBatch, alpha, activeDimension);
        PlaceholderPlayerRenderer.Draw(spriteBatch, alpha, activeDimension);
        PlaceholderProjectileRenderer.Draw(spriteBatch, alpha, activeDimension);

        spriteBatch.End();

        game.GraphicsDevice.SetRenderTarget(null);
        game.GraphicsDevice.Clear(Color.Black);

        float scaleX = (float)game.GraphicsDevice.PresentationParameters.BackBufferWidth / VirtualWidth;
        float scaleY = (float)game.GraphicsDevice.PresentationParameters.BackBufferHeight / VirtualHeight;
        float scale = Math.Min(scaleX, scaleY);

        int newW = (int)(VirtualWidth * scale);
        int newH = (int)(VirtualHeight * scale);
        var destRect = new Rectangle((game.GraphicsDevice.PresentationParameters.BackBufferWidth - newW) / 2, (game.GraphicsDevice.PresentationParameters.BackBufferHeight - newH) / 2, newW, newH);

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
        spriteBatch.Draw(virtualRenderTarget, destRect, Color.White);
        pauseMenu.Draw(spriteBatch);
        spriteBatch.End();
    }
}
