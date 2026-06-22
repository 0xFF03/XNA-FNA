using System;
using Flecs.NET.Core;
using Steamworks;
using Steamworks.Data;
using MemoryPack;
using MyGame.Prefabs;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Combat;
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
                if (!SteamManager.IsSteamActive) return;

                while (SteamNetworking.IsP2PPacketAvailable(0))
                {
                    var packet = SteamNetworking.ReadP2PPacket(0);
                    if (packet.HasValue) ProcessTransformPacket(_.World(), packet.Value);
                }

                while (SteamNetworking.IsP2PPacketAvailable(1))
                {
                    var packet = SteamNetworking.ReadP2PPacket(1);
                    if (packet.HasValue)
                    {
                        if (packet.Value.Data.Length == 0) continue;

                        byte type = packet.Value.Data[0];
                        if (type == PacketTypes.DistributedEvent)
                            ProcessDistributedEvent(_.World(), packet.Value);
                        else if (type == PacketTypes.WorldStateSnapshot)
                            ProcessWorldSnapshot(_.World(), packet.Value);
                        else
                            ProcessSpawnOrProjectilePacket(_.World(), packet.Value);
                    }
                }
            });
    }

    private static void ProcessWorldSnapshot(World world, P2Packet packet)
    {
        var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
        var p = MemoryPackSerializer.Deserialize<WorldStatePacket>(payloadSpan);

        int spawnedCount = 0;
        foreach (var entityData in p.Entities)
        {
            Entity existing = NetworkRegistry.GetEntity(entityData.NetworkId);

            // ARCHITECTURE FIX: Replaced != null with safe ID and Liveness check
            if (existing.Id != 0 && existing.IsAlive()) continue;

            if (entityData.EntityType == 0)
            {
                var mockTransform = new PlayerTransformPacket {
                    X = entityData.X,
                    Y = entityData.Y,
                    EntityNetworkSequenceId = entityData.NetworkId,
                    FacingDirection = entityData.FacingDirection,
                    TargetPhysicsWorld = entityData.TargetPhysicsWorld
                };

                var newProxy = PlayerFactory.CreateRemote(world, $"p_{entityData.NetworkId}", mockTransform, entityData.OwnerSteamId, entityData.TargetPhysicsWorld);
                if (newProxy.IsAlive())
                {
                    newProxy.Set(new BaseCombatComponents.Health { Current = entityData.Health, Max = 100 });
                    spawnedCount++;
                }
            }
        }
    }

    private static void ProcessDistributedEvent(Flecs.NET.Core.World world, P2Packet packet)
    {
        var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
        var p = MemoryPackSerializer.Deserialize<DistributedEventPacket>(payloadSpan);

        // ARCHITECTURE FIX: Removed nullable struct wrapper
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
            }
        }
    }

    private static void ProcessTransformPacket(Flecs.NET.Core.World world, P2Packet packet)
    {
        if (packet.SteamId == SteamClient.SteamId) return;

        var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
        var p = MemoryPackSerializer.Deserialize<PlayerTransformPacket>(payloadSpan);

        if (p.EntityNetworkSequenceId == 0) return;

        // ARCHITECTURE FIX: Removed nullable struct wrapper
        Entity remoteShadow = NetworkRegistry.GetEntity(p.EntityNetworkSequenceId);

        if (remoteShadow.Id == 0 || !remoteShadow.IsAlive())
        {
           remoteShadow = PlayerFactory.CreateRemote(world, $"p_{p.EntityNetworkSequenceId}", p, packet.SteamId, p.TargetPhysicsWorld);
        }

        if (remoteShadow.Id != 0 && remoteShadow.IsAlive())
        {
            if (!remoteShadow.Has<NetworkSequence>()) remoteShadow.Add<NetworkSequence>();

            ref var currentSequence = ref remoteShadow.GetMut<NetworkSequence>();
            if (p.SequenceNumber < currentSequence.LatestSequence) return;

            currentSequence.LatestSequence = p.SequenceNumber;
            currentSequence.TimeSinceLastPacket = 0f;

            remoteShadow.Set(new TargetPosition { X = p.X, Y = p.Y });
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
                        SpawnX = p.X,
                        SpawnY = p.Y
                    });
                }
            }
        }
    }

    private static void ProcessSpawnOrProjectilePacket(Flecs.NET.Core.World world, P2Packet packet)
    {
        if (packet.Data[0] == PacketTypes.Spawn)
        {
            var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
            var p = MemoryPackSerializer.Deserialize<PlayerSpawnPacket>(payloadSpan);

            Entity existing = NetworkRegistry.GetEntity(p.EntityNetworkSequenceId);

            // ARCHITECTURE FIX: Replaced == null with proper struct state check
            if (p.EntityNetworkSequenceId != 0 && (existing.Id == 0 || !existing.IsAlive()))
            {
                var mockTransform = new PlayerTransformPacket { X = p.StartX, Y = p.StartY, EntityNetworkSequenceId = p.EntityNetworkSequenceId };
                PlayerFactory.CreateRemote(world, $"p_{p.EntityNetworkSequenceId}", mockTransform, packet.SteamId, p.TargetPhysicsWorld);
            }
        }
        else if (packet.Data[0] == PacketTypes.ProjectileSpawn)
        {
            var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
            var p = MemoryPackSerializer.Deserialize<ProjectileSpawnPacket>(payloadSpan);
            ProjectileFactory.Create(world, p.StartX, p.StartY, p.VelocityX, p.VelocityY, p.EntityNetworkSequenceId, packet.SteamId, p.TargetPhysicsWorld);
        }
    }
}
