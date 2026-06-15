using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using Steamworks;
using MyGame.Engine.Networking;
using MyGame.Gameplay.Components;
using MyGame.Gameplay.Prefabs;

namespace MyGame.Gameplay.Systems;

public static class NetworkReceiverSystem
{
    private static readonly Dictionary<ulong, Entity> NetworkShadows = new();

    public static void ClearShadows()
    {
        NetworkShadows.Clear();
    }

    public static void Register(Flecs.NET.Core.World world)
    {
        world.System("NetworkReceiverSystem")
            .Kind(Ecs.PreUpdate)
            .Iter((Iter _) =>
            {
                if (!SteamManager.IsSteamActive) return;

                while (SteamNetworking.IsP2PPacketAvailable(0))
                {
                    var packetData = SteamNetworking.ReadP2PPacket(0);
                    if (!packetData.HasValue) continue;

                    byte[] buffer = packetData.Value.Data;
                    SteamId senderId = packetData.Value.SteamId;

                    if (buffer.Length == 0) continue;

                    if (buffer[0] == PacketTypes.Transform)
                    {
                        ProcessTransformPacket(_.World(), buffer, senderId);
                    }
                }

                while (SteamNetworking.IsP2PPacketAvailable(1))
                {
                    var packetData = SteamNetworking.ReadP2PPacket(1);
                    if (!packetData.HasValue) continue;

                    byte[] buffer = packetData.Value.Data;
                    SteamId senderId = packetData.Value.SteamId;

                    if (buffer.Length == 0) continue;

                    if (buffer[0] == PacketTypes.Spawn)
                    {
                        ProcessSpawnPacket(_.World(), buffer, senderId);
                    }
                }
            });
    }

    private static void ProcessTransformPacket(Flecs.NET.Core.World world, byte[] buffer, SteamId senderId)
    {
        if (senderId == SteamClient.SteamId) return;

        var packet = PlayerTransformPacket.Deserialize(buffer);
        if (packet.EntityNetworkSequenceId == 0) return;

        ulong netId = packet.EntityNetworkSequenceId;

        if (!NetworkShadows.TryGetValue(netId, out Entity remoteShadow)) return;

        if (!remoteShadow.IsAlive())
        {
            NetworkShadows.Remove(netId);
            return;
        }

        ref var currentSequence = ref remoteShadow.GetMut<NetworkSequence>();
        if (packet.SequenceNumber < currentSequence.LatestSequence) return;

        currentSequence.LatestSequence = packet.SequenceNumber;
        remoteShadow.Set(new TargetPosition { X = packet.X, Y = packet.Y });
        remoteShadow.Set(new Velocity { X = packet.Vx, Y = packet.Vy });
    }

    private static void ProcessSpawnPacket(Flecs.NET.Core.World world, byte[] buffer, SteamId senderId)
    {
        if (senderId == SteamClient.SteamId) return;

        var packet = PlayerSpawnPacket.Deserialize(buffer);
        if (packet.EntityNetworkSequenceId == 0) return;

        ulong netId = packet.EntityNetworkSequenceId;

        if (NetworkShadows.TryGetValue(netId, out Entity existingEntity))
        {
            if (existingEntity.IsAlive()) return;
            NetworkShadows.Remove(netId);
        }

        string entityLookupName = $"p_{netId}";

        var mockTransform = new PlayerTransformPacket
        {
            X = packet.StartX,
            Y = packet.StartY,
            Vx = 0,
            Vy = 0,
            CharacterClassId = packet.CharacterClassId,
            EntityNetworkSequenceId = packet.EntityNetworkSequenceId
        };

        Entity newShadow = PlayerFactory.CreateRemote(world, entityLookupName, mockTransform, senderId);
        NetworkShadows[netId] = newShadow;

        Console.WriteLine($"[Network Handshake]: Spawned verified remote entity shadow proxy: {entityLookupName}");
    }
}
