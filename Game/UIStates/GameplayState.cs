using System;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
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
using MemoryPack;
using MyGame.Game.Core;
using MyGame.Prefabs;
using MyGame.Game.Logic;

using XnaColor = Microsoft.Xna.Framework.Color;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Game.UIStates;

public class GameplayState : GameState
{
    private readonly Flecs.NET.Core.World ecsWorld;

    private PauseMenuOverlay pauseMenu = null!;
    private Camera2D camera = null!;
    private RenderTarget2D virtualRenderTarget = null!;

    public const int VirtualWidth = 480;
    public const int VirtualHeight = 270;
    private readonly bool isHostOrigin;

    private Query<Position, PreviousPosition, PhysicsDimension, BaseCombatComponents.Health> _localPlayerQuery;
    private Engine.Platform.LevelData _loadedRoom = null!;

    private static float _drawAlpha;
    private static Camera2D _drawCamera = null!;
    private static readonly List<Entity> _cleanupList = new(256);

    private float _sessionPlaytimeAccumulator = 0f;
    private float _autoSaveTimer = 0f;

    public bool IsSimulationPaused => pauseMenu != null && pauseMenu.IsPaused;

    public GameplayState(Game1 game, StateManager stateManager, Flecs.NET.Core.World sharedWorld)
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

        LoadMapAndPhysics();
    }

    private void HandleManualSave(int slotId)
    {
        _localPlayerQuery.Each((ref Position pos, ref PreviousPosition prev, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
        {
            var profile = SaveManager.CurrentProfile!;
            SaveManager.SaveToSlot(slotId, $"Manual Save {slotId}", profile.CurrentMapPath, pos.X, pos.Y, hp.Current, dim.Name, _sessionPlaytimeAccumulator);
            _sessionPlaytimeAccumulator = 0f;
        });
    }

    private void PerformAutoSave()
    {
        _localPlayerQuery.Each((ref Position pos, ref PreviousPosition prev, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
        {
            var profile = SaveManager.CurrentProfile!;
            SaveManager.PerformAutoSave(profile.CurrentMapPath, pos.X, pos.Y, hp.Current, dim.Name, _sessionPlaytimeAccumulator);
            _sessionPlaytimeAccumulator = 0f;
        });
    }

    private void HandleLoadRequest(int slotId)
    {
        SaveManager.LoadProfile(slotId);
        stateManager.ChangeState(new GameplayState(game, stateManager, ecsWorld));
    }

    private void LoadMapAndPhysics()
    {
        var profile = SaveManager.CurrentProfile!;
        _loadedRoom = Engine.Platform.MapLoader.LoadSingleLevel(profile.CurrentMapPath, profile.CurrentDimension);
        ecsWorld.Entity("GlobalMapData").Set(new MapComponents.MapInstance { Data = _loadedRoom });

        var macroGravity = _loadedRoom.IsTopDown ? new AetherVector2(0f, 0f) : new AetherVector2(0f, 20f);
        var macroWorld = PhysicsWorldManager.CreateWorld(profile.CurrentDimension, macroGravity);

        var levelStaticBody = macroWorld.CreateBody(AetherVector2.Zero, 0f, nkast.Aether.Physics2D.Dynamics.BodyType.Static);
        foreach (var col in _loadedRoom.Collisions)
        {
            float physX = (col.X + col.Width / 2f) / PhysicsSettings.PixelsPerMeter;
            float physY = (col.Y + col.Height / 2f) / PhysicsSettings.PixelsPerMeter;
            float physW = col.Width / PhysicsSettings.PixelsPerMeter;
            float physH = col.Height / PhysicsSettings.PixelsPerMeter;

            var fixture = levelStaticBody.CreateRectangle(physW, physH, 1f, new AetherVector2(physX, physY));
            fixture.Friction = 0.3f;
            fixture.CollisionCategories = PhysicsLayers.Environment;
        }

        SpawnMapEntities(profile.CurrentDimension);

        float spawnX = profile.CheckpointX != -1 ? profile.CheckpointX : _loadedRoom.SpawnPoint.X;
        float spawnY = profile.CheckpointY != -1 ? profile.CheckpointY : _loadedRoom.SpawnPoint.Y;

        ulong myNetId = NetworkIdGenerator.GetNextNetworkId();
        Entity localAvatar = PlayerFactory.CreateLocal(ecsWorld, profile.CharacterClassId, myNetId, spawnX, spawnY, profile.CurrentDimension);

        if (SteamManager.IsSteamActive && SteamManager.CurrentLobby.HasValue)
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

            foreach (var peer in SteamManager.CurrentLobby.Value.Members)
            {
                if (peer.Id != SteamClient.SteamId)
                    SteamNetworking.SendP2PPacket(peer.Id, networkBuffer, networkBuffer.Length, 1, P2PSend.Reliable);
            }

            if (!isHostOrigin && SteamManager.KnownHostId.HasValue)
            {
                byte[] readySignal = new byte[] { PacketTypes.ClientLoadedMap };
                SteamNetworking.SendP2PPacket(SteamManager.KnownHostId.Value, readySignal, 1, 2, P2PSend.Reliable);
            }
        }
    }

    private void SpawnMapEntities(string activeDimension)
    {
        foreach (var interactable in _loadedRoom.Interactables)
        {
            if (interactable.Identifier == "ShipExterior")
            {
                string dest = "Ship_Interior";
                Vector2 localDoorOffset = Vector2.Zero;

                if (interactable.FieldInstances != null)
                {
                    var field = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("TargetDimension", StringComparison.OrdinalIgnoreCase));
                    if (field != null && field.Value != null)
                    {
                        dest = field.Value is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()! : field.Value.ToString()!;
                    }

                    var offsetField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("DoorOffset", StringComparison.OrdinalIgnoreCase));
                    if (offsetField != null && offsetField.Value != null && offsetField.Value is JsonElement offsetJe && offsetJe.ValueKind == JsonValueKind.Object)
                    {
                        if (offsetJe.TryGetProperty("cx", out JsonElement cxProp) && offsetJe.TryGetProperty("cy", out JsonElement cyProp))
                        {
                            float absoluteDoorX = (cxProp.GetSingle() * 16f) + 8f;
                            float absoluteDoorY = (cyProp.GetSingle() * 16f) + 8f;

                            localDoorOffset = new Vector2(absoluteDoorX - interactable.Px[0], absoluteDoorY - interactable.Px[1]);
                        }
                    }
                }

                var vehicle = ecsWorld.Entity("ActiveSpaceshipExterior")
                    .Add<TopDownTag>()
                    .Add<MatchEntityTag>()
                    .Add<InteractableTag>()
                    .Set(new Position { X = interactable.Px[0], Y = interactable.Px[1] })
                    .Set(new PreviousPosition { X = interactable.Px[0], Y = interactable.Px[1] })
                    .Set(new Velocity { X = 0, Y = 0 })
                    .Set(new MovementCapabilities { MoveSpeed = 12f, JumpForce = 0 })
                    .Set(new PortalComponent { DestinationDimension = dest })
                    .Set(new ShipVehicleComponent { TextureName = "Textures/ShipExterior.png", DoorLocalOffset = localDoorOffset })
                    .Set(new PhysicsDimension { Name = activeDimension });

                var physicsWorld = PhysicsWorldManager.GetWorld(activeDimension);
                var initialPos = new AetherVector2(interactable.Px[0] / PhysicsSettings.PixelsPerMeter, interactable.Px[1] / PhysicsSettings.PixelsPerMeter);
                var aetherBody = physicsWorld.CreateCircle(16f / PhysicsSettings.PixelsPerMeter, 1f, initialPos, nkast.Aether.Physics2D.Dynamics.BodyType.Dynamic);
                aetherBody.FixedRotation = true;

                vehicle.Set(new PhysicsComponents.PhysicsBody { Value = aetherBody });
                continue;
            }

            var entity = ecsWorld.Entity()
                .Add<MatchEntityTag>()
                .Add<InteractableTag>()
                .Set(new Position { X = interactable.Px[0], Y = interactable.Px[1] });

            if (interactable.Identifier == "AirlockDoor")
            {
                string dest = "MacroSpace";
                if (interactable.FieldInstances != null && interactable.FieldInstances.Length > 0)
                {
                    var field = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("TargetDimension", StringComparison.OrdinalIgnoreCase));
                    if (field != null && field.Value != null)
                    {
                        dest = field.Value is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()! : field.Value.ToString()!;
                    }
                }
                entity.Set(new PortalComponent { DestinationDimension = dest });
            }
            else if (interactable.Identifier == "PilotSeat")
            {
                entity.Add<PilotSeatComponent>();
            }
        }
    }

    private void HotReloadVisualMap(string targetDimension)
    {
        AssetManager.UnloadLevelAssets();

        var profile = SaveManager.CurrentProfile!;
        _loadedRoom = Engine.Platform.MapLoader.LoadSingleLevel(profile.CurrentMapPath, targetDimension);
        ecsWorld.Entity("GlobalMapData").Set(new MapComponents.MapInstance { Data = _loadedRoom });

        _cleanupList.Clear();
        using var cleanupQuery = ecsWorld.QueryBuilder().With<InteractableTag>().Build();
        cleanupQuery.Each((Entity e) => { _cleanupList.Add(e); });

        for (int i = 0; i < _cleanupList.Count; i++)
        {
            if (_cleanupList[i].IsAlive()) _cleanupList[i].Destruct();
        }
        _cleanupList.Clear();

        SpawnMapEntities(targetDimension);

        EngineLogger.Log($"Graphics Renderer successfully hot-swapped to dimension visual assets: {targetDimension}", "SYSTEM");
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

        _cleanupList.Clear();
        using var cleanupQuery = ecsWorld.QueryBuilder().With<MatchEntityTag>().Build();
        cleanupQuery.Each((Entity e) => { _cleanupList.Add(e); });

        for (int i = 0; i < _cleanupList.Count; i++)
        {
            if (_cleanupList[i].IsAlive()) _cleanupList[i].Destruct();
        }
        _cleanupList.Clear();

        var mapEntity = ecsWorld.Entity("GlobalMapData");
        if (mapEntity.Has<MapComponents.MapInstance>()) mapEntity.Remove<MapComponents.MapInstance>();

        virtualRenderTarget?.Dispose();
        AssetManager.UnloadLevelAssets();
        PhysicsWorldManager.ClearAll();
        NetworkRegistry.ClearAll();
    }

    public override void Update(float deltaTime)
    {
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
            _sessionPlaytimeAccumulator += deltaTime;

            if (isHostOrigin)
            {
                _autoSaveTimer += deltaTime;
                if (_autoSaveTimer >= 300f)
                {
                    _autoSaveTimer = 0f;
                    PerformAutoSave();
                }
            }

            PhysicsWorldManager.StepAll(deltaTime);
            ecsWorld.Progress(deltaTime);

            _localPlayerQuery.Each((Entity player, ref Position pos, ref PreviousPosition prev, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
            {
                var profile = SaveManager.CurrentProfile!;
                if (profile.CurrentDimension != dim.Name)
                {
                    profile.CurrentDimension = dim.Name;
                    HotReloadVisualMap(dim.Name);
                }
            });
        }
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        _drawAlpha = alpha;
        _drawCamera = camera;

        _localPlayerQuery.Each((Entity player, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
        {
            float targetX = pos.X;
            float targetY = pos.Y;
            float prevTargetX = prevPos.X;
            float prevTargetY = prevPos.Y;

            // ARCHITECTURE FIX: Detach camera from player and lock onto the exterior ship when piloting
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
            _drawCamera.Position = new Vector2((int)lerpX, (int)lerpY);
        });

        game.GraphicsDevice.SetRenderTarget(virtualRenderTarget);
        game.GraphicsDevice.Clear(XnaColor.FromNonPremultiplied(40, 35, 50, 255));

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, camera.GetViewMatrix(VirtualWidth, VirtualHeight));

        TileMapRenderer.Draw(camera, VirtualWidth, VirtualHeight);
        PlaceholderVehicleRenderer.Draw(spriteBatch, alpha);
        PlaceholderPlayerRenderer.Draw(spriteBatch, alpha);
        PlaceholderProjectileRenderer.Draw(spriteBatch, alpha);

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
