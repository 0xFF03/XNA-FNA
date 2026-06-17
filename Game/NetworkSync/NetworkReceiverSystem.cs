using System;
using Flecs.NET.Core;
using Steamworks;
using Steamworks.Data;
using MyGame.Engine.Networking;
using MyGame.Prefabs;
using MemoryPack;
using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;
using MyGame.Game.Environment;

namespace MyGame.Game.NetworkSync;

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
                        if (packet.Value.Data[0] == PacketTypes.DistributedEvent)
                            ProcessDistributedEvent(_.World(), packet.Value);
                        else
                            ProcessSpawnOrProjectilePacket(_.World(), packet.Value);
                    }
                }
            });
    }

    private static void ProcessDistributedEvent(Flecs.NET.Core.World world, P2Packet packet)
    {
        if (packet.SteamId == SteamClient.SteamId || packet.Data.Length == 0) return;

        var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
        var p = MemoryPackSerializer.Deserialize<DistributedEventPacket>(payloadSpan);

        Entity? targetEntity = NetworkRegistry.GetEntity(p.TargetNetworkId);
        if (targetEntity.HasValue && targetEntity.Value.IsAlive())
        {
            Entity entity = targetEntity.Value;

            switch ((GameEventType)p.EventType)
            {
                case GameEventType.Despawn:
                    entity.Destruct();
                    break;
                case GameEventType.Damage:
                    if (entity.Has<Health>())
                    {
                        ref var health = ref entity.GetMut<Health>();
                        health.Current -= p.IntPayload;
                    }
                    break;
            }
        }
    }

    private static void ProcessTransformPacket(Flecs.NET.Core.World world, P2Packet packet)
    {
        if (packet.SteamId == SteamClient.SteamId) return;
        if (packet.Data.Length == 0 || packet.Data[0] != PacketTypes.Transform) return;

        var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
        var p = MemoryPackSerializer.Deserialize<PlayerTransformPacket>(payloadSpan);

        if (p.EntityNetworkSequenceId == 0) return;

        // 1. Attempt to get the entity from our NetworkRegistry
        Entity? remoteShadow = NetworkRegistry.GetEntity(p.EntityNetworkSequenceId);

        // 2. If it doesn't exist, spawn it
        if (!remoteShadow.HasValue || !remoteShadow.Value.IsAlive())
        {
           remoteShadow = PlayerFactory.CreateRemote(world, $"p_{p.EntityNetworkSequenceId}", p, packet.SteamId);
        }

        // 3. Final Guard Clause: Ensure entity is valid and alive before interacting
        if (remoteShadow is { } entity && entity.IsAlive())
        {
            // CRASH FIX: Ensure component exists before getting a mutable reference
            if (!entity.Has<NetworkSequence>())
            {
                entity.Add<NetworkSequence>();
            }

            ref var currentSequence = ref entity.GetMut<NetworkSequence>();

            // Ignore stale packets
            if (p.SequenceNumber < currentSequence.LatestSequence) return;

            currentSequence.LatestSequence = p.SequenceNumber;
            currentSequence.TimeSinceLastPacket = 0f;

            // Use entity.Set() - this acts as an "Upsert" (Adds component if missing, then updates it)
            // This is the safest way to update data in Flecs.NET
            entity.Set(new TargetPosition { X = p.X, Y = p.Y });
            entity.Set(new Velocity { X = p.Vx, Y = p.Vy });
            entity.Set(new FacingDirection { Value = p.FacingDirection });
        }
    }

    private static void ProcessSpawnOrProjectilePacket(Flecs.NET.Core.World world, P2Packet packet)
    {
        if (packet.SteamId == SteamClient.SteamId || packet.Data.Length == 0) return;

        if (packet.Data[0] == PacketTypes.Spawn)
        {
            var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
            var p = MemoryPackSerializer.Deserialize<PlayerSpawnPacket>(payloadSpan);

            if (p.EntityNetworkSequenceId != 0 && NetworkRegistry.GetEntity(p.EntityNetworkSequenceId) == null)
            {
                var mockTransform = new PlayerTransformPacket { X = p.StartX, Y = p.StartY, EntityNetworkSequenceId = p.EntityNetworkSequenceId };
                PlayerFactory.CreateRemote(world, $"p_{p.EntityNetworkSequenceId}", mockTransform, packet.SteamId);
            }
        }
        else if (packet.Data[0] == PacketTypes.ProjectileSpawn)
        {
            var payloadSpan = new ReadOnlySpan<byte>(packet.Data, 1, packet.Data.Length - 1);
            var p = MemoryPackSerializer.Deserialize<ProjectileSpawnPacket>(payloadSpan);
            ProjectileFactory.Create(world, p.StartX, p.StartY, p.VelocityX, p.VelocityY, p.EntityNetworkSequenceId, packet.SteamId);
        }
    }
}
