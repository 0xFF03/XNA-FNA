using System;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using Steamworks;
using MemoryPack;
using MyGame.Engine.Core;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Game.Core;
using MyGame.Prefabs;
using MyGame.Game.Logic;

using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Engine.Platform;

public static class LevelManager
{
    private static readonly Dictionary<string, LevelData> _levelCache = new();
    private static Query<InteractableTag> _interactableQuery;

    public static void Initialize(World ecsWorld)
    {
        _interactableQuery = ecsWorld.QueryBuilder<InteractableTag>().Build();
    }

    public static void UnloadAll()
    {
        _levelCache.Clear();
    }

    public static void LoadInitialWorld(World ecsWorld, SaveProfile profile, bool isHostOrigin)
    {
        LoadAndCacheDimension(ecsWorld, profile.CurrentMapPath, profile.CurrentDimension);

        var loadedRoom = _levelCache[profile.CurrentDimension];
        ecsWorld.Entity("GlobalMapData").Set(new MapComponents.MapInstance { Data = loadedRoom });

        float spawnX = profile.CheckpointX != -1 ? profile.CheckpointX : loadedRoom.SpawnPoint.X;
        float spawnY = profile.CheckpointY != -1 ? profile.CheckpointY : loadedRoom.SpawnPoint.Y;

        ulong myNetId = NetworkIdGenerator.GetNextNetworkId();
        PlayerFactory.CreateLocal(ecsWorld, profile.CharacterClassId, myNetId, spawnX, spawnY, profile.CurrentDimension);

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

    public static void HotReloadVisualMap(World ecsWorld, string targetDimension, Entity localPlayer, string leavingDimension)
    {
        LoadAndCacheDimension(ecsWorld, SaveManager.CurrentProfile!.CurrentMapPath, targetDimension);

        var loadedRoom = _levelCache[targetDimension];
        ecsWorld.Entity("GlobalMapData").Set(new MapComponents.MapInstance { Data = loadedRoom });

        float spawnX = loadedRoom.SpawnPoint.X;
        float spawnY = loadedRoom.SpawnPoint.Y;

        foreach (var interactable in loadedRoom.Interactables)
        {
            if (interactable.FieldInstances != null)
            {
                var destField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("TargetDimension", StringComparison.OrdinalIgnoreCase));
                if (destField != null && destField.Value != null)
                {
                    string destStr = destField.Value is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()! : destField.Value.ToString()!;

                    if (destStr == leavingDimension)
                    {
                        spawnX = interactable.Px[0];
                        spawnY = interactable.Px[1] + 16f;

                        var offsetField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("DoorOffset", StringComparison.OrdinalIgnoreCase));
                        if (offsetField != null && offsetField.Value != null && offsetField.Value is JsonElement offsetJe && offsetJe.ValueKind == JsonValueKind.Object)
                        {
                            if (offsetJe.TryGetProperty("cx", out JsonElement cxProp) && offsetJe.TryGetProperty("cy", out JsonElement cyProp))
                            {
                                spawnX = (cxProp.GetSingle() * 16f) + 8f;
                                spawnY = (cyProp.GetSingle() * 16f) + 24f;
                            }
                        }
                        break;
                    }
                }
            }
        }

        localPlayer.Set(new Position { X = spawnX, Y = spawnY });
        localPlayer.Set(new PreviousPosition { X = spawnX, Y = spawnY });

        if (localPlayer.Has<PhysicsComponents.PhysicsBody>())
        {
            var pBody = localPlayer.Get<PhysicsComponents.PhysicsBody>();
            if (pBody.Value != null)
            {
                pBody.Value.Position = new nkast.Aether.Physics2D.Common.Vector2(spawnX / PhysicsSettings.PixelsPerMeter, spawnY / PhysicsSettings.PixelsPerMeter);
            }
        }

        EngineLogger.Log($"Graphics Renderer successfully hot-swapped to dimension visual assets: {targetDimension}", "SYSTEM");
    }

    private static void LoadAndCacheDimension(World ecsWorld, string mapPath, string targetDimension)
    {
        if (_levelCache.ContainsKey(targetDimension)) return;

        var loadedRoom = MapLoader.LoadSingleLevel(mapPath, targetDimension);
        _levelCache[targetDimension] = loadedRoom;

        var macroGravity = loadedRoom.IsTopDown ? new AetherVector2(0f, 0f) : new AetherVector2(0f, 20f);
        var macroWorld = PhysicsWorldManager.CreateWorld(targetDimension, macroGravity);

        var levelStaticBody = macroWorld.CreateBody(AetherVector2.Zero, 0f, nkast.Aether.Physics2D.Dynamics.BodyType.Static);
        foreach (var col in loadedRoom.Collisions)
        {
            float physX = (col.X + col.Width / 2f) / PhysicsSettings.PixelsPerMeter;
            float physY = (col.Y + col.Height / 2f) / PhysicsSettings.PixelsPerMeter;
            float physW = col.Width / PhysicsSettings.PixelsPerMeter;
            float physH = col.Height / PhysicsSettings.PixelsPerMeter;

            var fixture = levelStaticBody.CreateRectangle(physW, physH, 1f, new AetherVector2(physX, physY));
            fixture.Friction = 0.3f;
            fixture.CollisionCategories = PhysicsLayers.Environment;
        }

        SpawnMapEntities(ecsWorld, loadedRoom, targetDimension);
    }

    private static void SpawnMapEntities(World ecsWorld, LevelData loadedRoom, string activeDimension)
    {
        ulong hostId = SteamManager.GetLocalOrHostId();

        foreach (var interactable in loadedRoom.Interactables)
        {
            ulong deterministicNetId = NetworkIdGenerator.GetDeterministicNetworkId(interactable.Iid);

            Entity existing = NetworkRegistry.GetEntity(deterministicNetId);
            if (existing.Id != 0 && existing.IsAlive()) continue;

            float startX = interactable.Px[0];
            float startY = interactable.Px[1];
            int savedInteractionState = 0;

            if (SaveManager.CurrentProfile != null && SaveManager.CurrentProfile.PersistentWorldMarks.TryGetValue(interactable.Iid, out var savedMark))
            {
                savedInteractionState = savedMark.State;
                if (savedMark.X != 0 || savedMark.Y != 0)
                {
                    startX = savedMark.X;
                    startY = savedMark.Y;
                }
            }

            bool hasDoorOffset = false;
            bool hasTargetDimension = false;
            Vector2 localDoorOffset = Vector2.Zero;
            string destDimension = "MacroSpace";
            string textureName = $"Textures/{interactable.Identifier}.png";

            if (interactable.FieldInstances != null)
            {
                var offsetField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("DoorOffset", StringComparison.OrdinalIgnoreCase));
                if (offsetField != null && offsetField.Value != null && offsetField.Value is JsonElement offsetJe && offsetJe.ValueKind == JsonValueKind.Object)
                {
                    hasDoorOffset = true;
                    if (offsetJe.TryGetProperty("cx", out JsonElement cxProp) && offsetJe.TryGetProperty("cy", out JsonElement cyProp))
                    {
                        float absoluteDoorX = (cxProp.GetSingle() * 16f) + 8f;
                        float absoluteDoorY = (cyProp.GetSingle() * 16f) + 8f;
                        localDoorOffset = new Vector2(absoluteDoorX - interactable.Px[0], absoluteDoorY - interactable.Px[1]);
                    }
                }

                var destField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("TargetDimension", StringComparison.OrdinalIgnoreCase));
                if (destField != null && destField.Value != null)
                {
                    hasTargetDimension = true;
                    destDimension = destField.Value is JsonElement je && je.ValueKind == JsonValueKind.String ? je.GetString()! : destField.Value.ToString()!;
                }
            }

            if (hasTargetDimension && hasDoorOffset)
            {
                var vehicle = ecsWorld.Entity()
                    .Add<TopDownTag>()
                    .Add<MatchEntityTag>()
                    .Add<InteractableTag>()
                    .Set(new Position { X = startX, Y = startY })
                    .Set(new PreviousPosition { X = startX, Y = startY })
                    .Set(new Velocity { X = 0, Y = 0 })
                    .Set(new MovementCapabilities { MoveSpeed = 12f, JumpForce = 0 })
                    .Set(new PortalComponent { DestinationDimension = destDimension })
                    .Set(new ShipVehicleComponent { TextureName = textureName, DoorLocalOffset = localDoorOffset })
                    .Set(new PhysicsDimension { Name = activeDimension })
                    .Set(new NetworkId { Value = deterministicNetId })
                    .Set(new NetworkOwner { Value = hostId })
                    .Set(new WorldMark { UniqueMarkId = interactable.Iid, InteractionState = savedInteractionState });

                var physicsWorld = PhysicsWorldManager.GetWorld(activeDimension);
                var initialPos = new AetherVector2(startX / PhysicsSettings.PixelsPerMeter, startY / PhysicsSettings.PixelsPerMeter);
                var aetherBody = physicsWorld.CreateCircle(16f / PhysicsSettings.PixelsPerMeter, 1f, initialPos, nkast.Aether.Physics2D.Dynamics.BodyType.Dynamic);
                aetherBody.FixedRotation = true;

                if (aetherBody.FixtureList.Count > 0)
                {
                    aetherBody.FixtureList[0].CollisionCategories = PhysicsLayers.LocalPlayer;
                    aetherBody.FixtureList[0].CollidesWith = PhysicsLayers.Environment | PhysicsLayers.EnemyAndProjectiles;
                }

                vehicle.Set(new PhysicsComponents.PhysicsBody { Value = aetherBody });
                NetworkRegistry.Add(deterministicNetId, vehicle);
                continue;
            }

            var entity = ecsWorld.Entity()
                .Add<MatchEntityTag>()
                .Add<InteractableTag>()
                .Set(new Position { X = startX, Y = startY })
                .Set(new NetworkId { Value = deterministicNetId })
                .Set(new NetworkOwner { Value = hostId })
                .Set(new WorldMark { UniqueMarkId = interactable.Iid, InteractionState = savedInteractionState });

            if (savedInteractionState > 0) entity.Remove<InteractableTag>();

            if (hasTargetDimension && !hasDoorOffset)
            {
                entity.Set(new PortalComponent { DestinationDimension = destDimension });
            }
            else if (interactable.Identifier == "PilotSeat")
            {
                entity.Add<PilotSeatComponent>();
            }
            else if (interactable.Identifier == "GunnerSeat")
            {
                entity.Add<GunnerSeatComponent>();
            }

            NetworkRegistry.Add(deterministicNetId, entity);
        }
    }
}
