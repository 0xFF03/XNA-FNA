using System;
using Flecs.NET.Core;
using MemoryPack;
using MyGame.Prefabs;
using MyGame.Engine.Platform.Networking;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.Core;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class NetworkReceiverSystem
{
    public static void Register(Flecs.NET.Core.World world)
    {
        world.System("NetworkReceiverSystem")
            .Kind(Ecs.PreUpdate)
            .Iter((Iter _) =>
            {
                var net = NetworkServiceLocator.Provider;
                if (!net.IsActive) return;

                while (net.TryReadPacket(0, out var packetUnreliable))
                {
                    try { ProcessTransformPacket(_.World(), packetUnreliable); }
                    catch (Exception ex) { EngineLogger.Log($"Failed parsing Transform (CH0): {ex.Message}", "WARNING"); }
                }

                while (net.TryReadPacket(1, out var packetReliable))
                {
                    if (packetReliable.Length == 0) continue;

                    byte type = packetReliable.Data[0];
                    try
                    {
                        if (type == PacketTypes.DistributedEvent)
                        {
                            ProcessDistributedEvent(_.World(), packetReliable);
                        }
                        else if (type == PacketTypes.WorldStateSnapshot)
                        {
                            EngineLogger.Log($"Received WorldStateSnapshot from {packetReliable.SenderId}", "NETWORK");
                            ProcessWorldSnapshot(_.World(), packetReliable);
                        }
                        else if (type == PacketTypes.Spawn || type == PacketTypes.ProjectileSpawn)
                        {
                            EngineLogger.Log($"Received Spawn Packet ({type}) from {packetReliable.SenderId}", "NETWORK");
                            ProcessSpawnOrProjectilePacket(_.World(), packetReliable);
                        }
                    }
                    catch (Exception ex)
                    {
                        EngineLogger.Log($"Failed parsing Reliable (CH1) Type {type}: {ex.Message}", "ERROR");
                    }
                }
            });
    }

    private static void ProcessWorldSnapshot(World world, NetworkPacket packet)
    {
        var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Length - 1);
        var p = MemoryPackSerializer.Deserialize<WorldStatePacket>(payloadSpan);

        EngineLogger.Log($"Deserialized World Snapshot containing {p.Entities.Length} entities.", "NETWORK");

        foreach (var entityData in p.Entities)
        {
            // ARCHITECTURE FIX: O(1) Snapshot application. Deleted the massive O(N) array loop query.
            if (entityData.EntityType == 255)
            {
                Entity markEnt = NetworkRegistry.GetEntity(entityData.NetworkId);
                if (markEnt.Id != 0 && markEnt.IsAlive() && markEnt.Has<WorldMark>())
                {
                    ref var mark = ref markEnt.GetMut<WorldMark>();
                    mark.InteractionState = entityData.Health;

                    if (markEnt.Has<Position>())
                    {
                        markEnt.Set(new Position { X = entityData.X, Y = entityData.Y, Rotation = entityData.Rotation });
                        markEnt.Set(new PreviousPosition { X = entityData.X, Y = entityData.Y, Rotation = entityData.Rotation });

                        if (markEnt.Has<PhysicsComponents.PhysicsBody>())
                        {
                            var pBody = markEnt.Get<PhysicsComponents.PhysicsBody>();
                            if (pBody.Value != null)
                            {
                                pBody.Value.Position = new nkast.Aether.Physics2D.Common.Vector2(
                                    entityData.X / PhysicsSettings.PixelsPerMeter,
                                    entityData.Y / PhysicsSettings.PixelsPerMeter);
                                pBody.Value.Rotation = entityData.Rotation;
                            }
                        }
                    }
                }
                continue;
            }

            Entity existing = NetworkRegistry.GetEntity(entityData.NetworkId);
            if (existing.Id != 0 && existing.IsAlive()) continue;

            if (entityData.EntityType == 0)
            {
                var mockTransform = new PlayerTransformPacket {
                    X = entityData.X,
                    Y = entityData.Y,
                    Rotation = entityData.Rotation,
                    EntityNetworkSequenceId = entityData.NetworkId,
                    FacingDirection = entityData.FacingDirection,
                    TargetPhysicsWorld = entityData.TargetPhysicsWorld
                };

                var newProxy = PlayerFactory.CreateRemote(world, $"p_{entityData.NetworkId}", mockTransform, entityData.OwnerSteamId, entityData.TargetPhysicsWorld);
                if (newProxy.IsAlive())
                {
                    newProxy.Set(new BaseCombatComponents.Health { Current = entityData.Health, Max = 100 });
                }
            }
        }
    }

    private static void ProcessDistributedEvent(Flecs.NET.Core.World world, NetworkPacket packet)
    {
        var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Length - 1);
        var p = MemoryPackSerializer.Deserialize<DistributedEventPacket>(payloadSpan);

        Entity targetEntity = NetworkRegistry.GetEntity(p.TargetNetworkId);

        if (targetEntity.Id != 0 && targetEntity.IsAlive())
        {
            switch ((GameEventType)p.EventType)
            {
                case GameEventType.Despawn:
                    targetEntity.Destruct();
                    break;
                case GameEventType.Damage:
                    if (targetEntity.Has<BaseCombatComponents.Health>())
                    {
                        ref var health = ref targetEntity.GetMut<BaseCombatComponents.Health>();
                        health.Current -= p.IntPayload;
                    }
                    break;
                case GameEventType.InteractSwitch:
                    if (targetEntity.Has<WorldMark>())
                    {
                        ref var mark = ref targetEntity.GetMut<WorldMark>();
                        mark.InteractionState = p.IntPayload;
                        if (p.IntPayload > 0) targetEntity.Remove<InteractableTag>();
                    }
                    break;
                case GameEventType.ClaimAuthority:
                    if (targetEntity.Has<NetworkOwner>())
                    {
                        targetEntity.Set(new NetworkOwner { Value = p.UlongPayload });
                        EngineLogger.Log($"Authority Claimed! Entity {p.TargetNetworkId} transferred to Peer {p.UlongPayload}", "NETWORK");
                    }
                    break;
            }
        }
    }

    private static void ProcessTransformPacket(Flecs.NET.Core.World world, NetworkPacket packet)
    {
        if (packet.SenderId == NetworkServiceLocator.Provider.LocalUserId) return;

        var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Length - 1);
        var p = MemoryPackSerializer.Deserialize<PlayerTransformPacket>(payloadSpan);

        if (p.EntityNetworkSequenceId == 0) return;

        Entity remoteShadow = NetworkRegistry.GetEntity(p.EntityNetworkSequenceId);

        if (remoteShadow.Id == 0 || !remoteShadow.IsAlive()) return;

        if (remoteShadow.Id != 0 && remoteShadow.IsAlive())
        {
            if (!remoteShadow.Has<NetworkSequence>()) remoteShadow.Add<NetworkSequence>();

            ref var currentSequence = ref remoteShadow.GetMut<NetworkSequence>();
            if (p.SequenceNumber < currentSequence.LatestSequence) return;

            currentSequence.LatestSequence = p.SequenceNumber;
            currentSequence.TimeSinceLastPacket = 0f;

            remoteShadow.Set(new TargetPosition { X = p.X, Y = p.Y, Rotation = p.Rotation });
            remoteShadow.Set(new Velocity { X = p.Vx, Y = p.Vy });
            remoteShadow.Set(new FacingDirection { Value = p.FacingDirection });

            if (remoteShadow.Has<PhysicsDimension>())
            {
                var currentDim = remoteShadow.Get<PhysicsDimension>();
                if (currentDim.Name != p.TargetPhysicsWorld)
                {
                    remoteShadow.Set(new DimensionTransferRequest
                    {
                        TargetDimension = p.TargetPhysicsWorld,
                        ExplicitSpawnX = p.X,
                        ExplicitSpawnY = p.Y
                    });
                }
            }
        }
    }

    private static void ProcessSpawnOrProjectilePacket(Flecs.NET.Core.World world, NetworkPacket packet)
    {
        if (packet.Data[0] == PacketTypes.Spawn)
        {
            var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Length - 1);
            var p = MemoryPackSerializer.Deserialize<PlayerSpawnPacket>(payloadSpan);

            Entity existing = NetworkRegistry.GetEntity(p.EntityNetworkSequenceId);

            if (p.EntityNetworkSequenceId != 0 && (existing.Id == 0 || !existing.IsAlive()))
            {
                var mockTransform = new PlayerTransformPacket { X = p.StartX, Y = p.StartY, Rotation = 0f, EntityNetworkSequenceId = p.EntityNetworkSequenceId };
                PlayerFactory.CreateRemote(world, $"p_{p.EntityNetworkSequenceId}", mockTransform, packet.SenderId, p.TargetPhysicsWorld);
                EngineLogger.Log($"Spawned Remote Player {p.EntityNetworkSequenceId} at {p.StartX}, {p.StartY}", "NETWORK");
            }
        }
        else if (packet.Data[0] == PacketTypes.ProjectileSpawn)
        {
            var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Length - 1);
            var p = MemoryPackSerializer.Deserialize<ProjectileSpawnPacket>(payloadSpan);
            ProjectileFactory.Create(world, p.StartX, p.StartY, p.VelocityX, p.VelocityY, p.EntityNetworkSequenceId, packet.SenderId, p.TargetPhysicsWorld);
        }
    }
}
