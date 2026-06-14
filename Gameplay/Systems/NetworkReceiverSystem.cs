using System;
using Flecs.NET.Core;
using Steamworks;
using MyGame.Engine.Networking;
using MyGame.Gameplay.Components;
using MyGame.Gameplay.Networking;
using MyGame.Gameplay.Prefabs;

namespace MyGame.Gameplay.Systems;

public static class NetworkReceiverSystem
{
    public static void Register(World world, RemoteSessionManager sessionManager)
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

                    byte packetType = buffer[0];

                    switch (packetType)
                    {
                        case PacketTypes.Transform:
                            ProcessTransformPacket(world, sessionManager, buffer, senderId);
                            break;
                    }
                }
            });
    }

    private static void ProcessTransformPacket(World world, RemoteSessionManager sessionManager, byte[] buffer, SteamId senderId)
    {
        var packet = PlayerTransformPacket.Deserialize(buffer);
        if (packet.EntityNetworkSequenceId == 0) return;

        ulong netId = packet.EntityNetworkSequenceId;

        if (!sessionManager.TryGetPlayer(netId, out Entity remoteShadow))
        {
            string entityName = $"RemoteProxy_{netId}";
            remoteShadow = PlayerFactory.CreateRemote(world, entityName, packet, senderId);
            sessionManager.RegisterPlayer(netId, remoteShadow);
        }
        else
        {
            // We fetch the current timeline sequence of this specific entity
            ref var currentSequence = ref remoteShadow.GetMut<NetworkSequence>();

            // If the incoming packet is older than what we already applied, DROP IT to prevent jitter.
            if (packet.SequenceNumber < currentSequence.LatestSequence) return;

            // Otherwise, update the timeline and apply the fresh physics
            currentSequence.LatestSequence = packet.SequenceNumber;

            remoteShadow.Set(new TargetPosition { X = packet.X, Y = packet.Y });
            remoteShadow.Set(new Velocity { X = packet.Vx, Y = packet.Vy });
        }
    }
}
