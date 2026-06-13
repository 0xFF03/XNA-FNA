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
        // FIX: In Flecs.NET v4, parameterless Iter systems use the explicit Iter(Action<Iter> callback) signature
        world.System("NetworkReceiverSystem")
            .Kind(Ecs.PreUpdate)
            .Iter((Iter it) =>
            {
                if (!SteamManager.IsSteamActive) return;

                while (SteamNetworking.IsP2PPacketAvailable(0))
                {
                    var packetData = SteamNetworking.ReadP2PPacket(0);
                    if (!packetData.HasValue) continue;

                    byte[] buffer = packetData.Value.Data;
                    SteamId senderId = packetData.Value.SteamId;

                    if (buffer.Length > 0 && buffer[0] == 1)
                    {
                        var packet = PlayerTransformPacket.Deserialize(buffer);
                        string entityUniqueKey = $"{senderId}_{packet.PacketType}";

                        if (!sessionManager.TryGetPlayer(entityUniqueKey, out Entity remoteShadow))
                        {
                            remoteShadow = PlayerFactory.CreateRemote(world, entityUniqueKey, packet, senderId);
                            sessionManager.RegisterPlayer(entityUniqueKey, remoteShadow);
                            Console.WriteLine($"[Network Sync]: Spawned remote shadow character: {entityUniqueKey}");
                        }
                        else
                        {
                            remoteShadow.Set(new Position { X = packet.X, Y = packet.Y });
                            remoteShadow.Set(new Velocity { X = packet.Vx, Y = packet.Vy });
                        }
                    }
                }
            });
    }
}
