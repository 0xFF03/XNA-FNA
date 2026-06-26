using System;
using MemoryPack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.Networking;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Game.Core;
using MyGame.Game.Logic;
using MyGame.Prefabs;
using MyGame.Game.Rendering;

using FlecsWorld = Flecs.NET.Core.World;
using FlecsEntity = Flecs.NET.Core.Entity;

namespace MyGame.Game.UIStates;

public class GameplayState : GameState
{
    private readonly FlecsWorld ecsWorld;
    private PauseMenuOverlay pauseMenu = null!;
    private RenderTarget2D virtualRenderTarget = null!;
    private GameplayRenderer sceneRenderer = null!;

    public const int VirtualWidth = 480;
    public const int VirtualHeight = 270;
    private readonly bool isHostOrigin;

    private Flecs.NET.Core.Query<Position, PreviousPosition, PhysicsDimension, BaseCombatComponents.Health> _localPlayerQuery;
    private Flecs.NET.Core.Query<LocalInput> _inputQuery;

    private ulong _activeInteriorVehicleId = 0;
    private string _activeExteriorDimension = "MacroSpace";
    private CameraViewMode _currentViewMode = CameraViewMode.InteriorCrew;
    private float _activeAltitudeRatio = 0f;

    private Vector2 _smoothedPanOffset = Vector2.Zero;

    public bool IsSimulationPaused => pauseMenu != null && pauseMenu.IsPaused;

    public GameplayState(Game1 game, StateManager stateManager, FlecsWorld sharedWorld)
        : base(game, stateManager)
    {
        ecsWorld = sharedWorld;

        var net = NetworkServiceLocator.Provider;
        isHostOrigin = !net.HostId.HasValue || net.HostId.Value == net.LocalUserId;

        if (SaveManager.CurrentProfile == null)
            EngineLogger.LogFatalSync("GameplayState initialized without an active save profile!", new Exception("SaveProfile Null"));
    }

    public override void LoadContent()
    {
        virtualRenderTarget = new RenderTarget2D(game.GraphicsDevice, VirtualWidth, VirtualHeight, false, SurfaceFormat.Color, DepthFormat.None);
        pauseMenu = new PauseMenuOverlay(game, stateManager, isHostOrigin);
        sceneRenderer = new GameplayRenderer();

        _localPlayerQuery = ecsWorld.QueryBuilder<Position, PreviousPosition, PhysicsDimension, BaseCombatComponents.Health>().With<LocalPlayerTag>().Build();
        _inputQuery = ecsWorld.QueryBuilder<LocalInput>().With<LocalPlayerTag>().Build();

        pauseMenu.OnSaveSlotRequested += HandleManualSave;
        pauseMenu.OnLoadSlotRequested += HandleLoadRequest;

        if (isHostOrigin) NetworkRouter.OnClientLoadedMap += HandleJoineeLoadedMap;

        LevelManager.OnEntityParsed = MapEntityFactory.BuildEntity;

        Engine.StandardModules.Rendering2D.PlaceholderVehicleRenderer.Initialize(ecsWorld);
        Engine.StandardModules.Rendering2D.PlaceholderPlayerRenderer.Initialize(ecsWorld);
        Engine.StandardModules.Rendering2D.PlaceholderProjectileRenderer.Initialize(ecsWorld);

        InitializeGameSession();
    }

    private void InitializeGameSession()
    {
        var profile = SaveManager.CurrentProfile!;

        LevelManager.EnsureDimensionLoaded(ecsWorld, profile.CurrentDimension);

        var loadedRoom = LevelManager.GetCachedLevel(profile.CurrentDimension);
        if (loadedRoom != null) ecsWorld.Entity("GlobalMapData").Set(new MapComponents.MapInstance { Data = loadedRoom });

        float spawnX = profile.CheckpointX != -1 ? profile.CheckpointX : loadedRoom?.SpawnPoint.X ?? 100f;
        float spawnY = profile.CheckpointY != -1 ? profile.CheckpointY : loadedRoom?.SpawnPoint.Y ?? 100f;

        ulong myNetId = NetworkIdGenerator.GetNextNetworkId();
        PlayerFactory.CreateLocal(ecsWorld, profile.CharacterClassId, myNetId, spawnX, spawnY, profile.CurrentDimension);

        var net = NetworkServiceLocator.Provider;
        if (net.IsActive && net.IsInLobby)
        {
            var handshakePayload = new PlayerSpawnPacket
            {
                CharacterClassId = profile.CharacterClassId,
                StartX = spawnX,
                StartY = spawnY,
                EntityNetworkSequenceId = myNetId,
                TargetPhysicsWorld = profile.CurrentDimension
            };

            byte[] payload = MemoryPackSerializer.Serialize(handshakePayload);
            byte[] networkBuffer = new byte[payload.Length + 1];
            networkBuffer[0] = PacketTypes.Spawn;
            Buffer.BlockCopy(payload, 0, networkBuffer, 1, payload.Length);

            net.BroadcastPacket(networkBuffer, networkBuffer.Length, 1, reliable: true);

            if (!isHostOrigin && net.HostId.HasValue)
            {
                byte[] readySignal = new byte[] { PacketTypes.ClientLoadedMap };
                net.SendPacket(net.HostId.Value, readySignal, 1, 2, reliable: true);
            }
        }
    }

    private void HandleManualSave(int slotId)
    {
        _localPlayerQuery.Each((FlecsEntity player, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
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

    private void HandleJoineeLoadedMap(ulong joineeId) => WorldSyncSystem.SendWorldSnapshot(ecsWorld, joineeId);

    public override void UnloadContent()
    {
        pauseMenu.OnSaveSlotRequested -= HandleManualSave;
        pauseMenu.OnLoadSlotRequested -= HandleLoadRequest;
        pauseMenu.Unload();

        if (isHostOrigin) NetworkRouter.OnClientLoadedMap -= HandleJoineeLoadedMap;

        LevelManager.OnEntityParsed = null;

        using var cleanupQuery = ecsWorld.QueryBuilder().With<MatchEntityTag>().Build();
        cleanupQuery.Each((FlecsEntity e) => { if (e.IsAlive()) e.Destruct(); });

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
        var net = NetworkServiceLocator.Provider;

        if (SaveManager.CurrentProfile == null)
        {
            net.LeaveLobby();
            stateManager.ChangeState(new MainMenuState(game, stateManager));
            return;
        }

        if (net.IsActive && net.IsInLobby && (!isHostOrigin && (!net.HostId.HasValue || net.HostId.Value == 0)))
        {
            net.LeaveLobby();
            stateManager.ChangeState(new MainMenuState(game, stateManager));
            return;
        }

        pauseMenu.Update();

        if (!IsSimulationPaused)
        {
            Camera2D activeCamera = _currentViewMode == CameraViewMode.ExteriorPiloting ? sceneRenderer.ExteriorCamera : sceneRenderer.Camera;
            Vector2 worldMouse = InputManager.GetWorldMousePosition(activeCamera, VirtualWidth, VirtualHeight, game.GraphicsDevice.PresentationParameters.BackBufferWidth, game.GraphicsDevice.PresentationParameters.BackBufferHeight);

            _inputQuery.Each((ref LocalInput input) =>
            {
                input.WorldMousePosition = worldMouse;
            });

            // ARCHITECTURE FIX: Fetch the exact altitude ratio dynamically so the Camera Pan can use it instantly
            _activeAltitudeRatio = 0f;
            _localPlayerQuery.Each((FlecsEntity player, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
            {
                if (player.Has<HelmControl>())
                {
                    var helm = player.Get<HelmControl>();
                    if (helm.ControlledVehicle.Id != 0 && helm.ControlledVehicle.Has<VehicleFlightState>())
                    {
                        _activeAltitudeRatio = helm.ControlledVehicle.Get<VehicleFlightState>().AltitudeRatio;
                    }
                }
            });

            if (_currentViewMode == CameraViewMode.ExteriorPiloting)
            {
                Point rawMouse = InputManager.GetScreenMousePosition();
                Vector2 screenCenter = new Vector2(game.GraphicsDevice.PresentationParameters.BackBufferWidth / 2f, game.GraphicsDevice.PresentationParameters.BackBufferHeight / 2f);

                // ARCHITECTURE FIX: Panning multiplier transitions from a gentle 10% on the ground to an aggressive 70% in deep space!
                float panMultiplier = MathHelper.Lerp(0.1f, 0.7f, _activeAltitudeRatio);
                Vector2 targetPanOffset = (new Vector2(rawMouse.X, rawMouse.Y) - screenCenter) * panMultiplier;
                _smoothedPanOffset = Vector2.Lerp(_smoothedPanOffset, targetPanOffset, 1f - MathF.Exp(-5f * deltaTime));
            }
            else
            {
                _smoothedPanOffset = Vector2.Lerp(_smoothedPanOffset, Vector2.Zero, 1f - MathF.Exp(-10f * deltaTime));
            }

            ecsWorld.Progress(deltaTime);

            _localPlayerQuery.Each((FlecsEntity player, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
            {
                var profile = SaveManager.CurrentProfile!;
                if (profile.CurrentDimension != dim.Name)
                {
                    profile.CurrentDimension = dim.Name;

                    LevelManager.EnsureDimensionLoaded(ecsWorld, dim.Name);
                    var loadedRoom = LevelManager.GetCachedLevel(dim.Name);
                    if (loadedRoom != null) ecsWorld.Entity("GlobalMapData").Set(new MapComponents.MapInstance { Data = loadedRoom });

                    int lastIdx = dim.Name.LastIndexOf('_');
                    if (lastIdx >= 0 && ulong.TryParse(dim.Name.Substring(lastIdx + 1), out ulong netId))
                    {
                        _activeInteriorVehicleId = netId;
                        FlecsEntity parentShip = NetworkRegistry.GetEntity(netId);
                        if (parentShip.IsAlive() && parentShip.Has<PhysicsDimension>())
                            _activeExteriorDimension = parentShip.Get<PhysicsDimension>().Name;
                    }
                    else
                    {
                        _activeInteriorVehicleId = 0;
                        _activeExteriorDimension = dim.Name;
                    }
                }
            });
        }
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        if (SaveManager.CurrentProfile == null) return;

        string interiorDimension = "MacroSpace";
        Vector2 targetRenderPos = Vector2.Zero;
        _currentViewMode = CameraViewMode.InteriorCrew;

        _localPlayerQuery.Each((FlecsEntity player, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
        {
            interiorDimension = dim.Name;

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

                    _currentViewMode = CameraViewMode.ExteriorPiloting;

                    targetX = vPos.X;
                    targetY = vPos.Y;
                    prevTargetX = vPrev.X;
                    prevTargetY = vPrev.Y;
                }
            }

            float lerpX = MathHelper.Lerp(prevTargetX, targetX, alpha);
            float lerpY = MathHelper.Lerp(prevTargetY, targetY, alpha);
            targetRenderPos = new Vector2(lerpX, lerpY);

            targetRenderPos += _smoothedPanOffset;
        });

        sceneRenderer.DrawScene(spriteBatch, game.GraphicsDevice, virtualRenderTarget, alpha, _currentViewMode, _activeAltitudeRatio, targetRenderPos, interiorDimension, _activeExteriorDimension, _activeInteriorVehicleId, VirtualWidth, VirtualHeight);

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
