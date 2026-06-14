using System;
using Flecs.NET.Core;
using Steamworks;
using MyGame.Engine.Networking;
using MyGame.Gameplay.Components;
using MyGame.Gameplay.Networking;

namespace MyGame.Gameplay.Systems;

public static class NetworkBroadcastSystem
{
    // each thread gets its own private buffer, completely preventing memory corruption.
    [ThreadStatic]
    private static byte[]? _transformBuffer;

    private static uint _outboundSequenceCounter = 0;

    public static void Register(World world)
    {
        world.System<Position, Velocity, CharacterClass, NetworkId>("NetworkBroadcastSystem")
            .Kind(Ecs.PostUpdate)
            .With<LocalPlayerTag>()
            .Interval(1f / 30f)
            .Each((Entity _, ref Position pos, ref Velocity vel, ref CharacterClass cClass, ref NetworkId netId) =>
            {
                if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

                // Lazy load the buffer per-thread without allocating in the hot-path
                _transformBuffer ??= new byte[System.Runtime.InteropServices.Marshal.SizeOf<PlayerTransformPacket>()];

                _outboundSequenceCounter++;

                var packet = new PlayerTransformPacket
                {
                    PacketType = PacketTypes.Transform,
                    SequenceNumber = _outboundSequenceCounter,
                    CharacterClassId = cClass.Id,
                    X = pos.X,
                    Y = pos.Y,
                    Vx = vel.X,
                    Vy = vel.Y,
                    EntityNetworkSequenceId = netId.Value
                };

                packet.SerializeTo(_transformBuffer);

                foreach (var member in SteamManager.CurrentLobby.Value.Members)
                {
                    if (member.Id == SteamClient.SteamId) continue;
                    SteamNetworking.SendP2PPacket(member.Id, _transformBuffer, _transformBuffer.Length, 0, P2PSend.Unreliable);
                }
            });
    }
}
