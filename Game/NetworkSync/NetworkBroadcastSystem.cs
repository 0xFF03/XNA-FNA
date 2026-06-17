using System;
using System.Buffers;
using Flecs.NET.Core;
using Steamworks;
using MyGame.Engine.Networking;
using MemoryPack;

using MyGame.Game.Core;
using MyGame.Game.Physics;

namespace MyGame.Game.NetworkSync;

public static class NetworkBroadcastSystem
{
    private static readonly ArrayBufferWriter<byte> _bufferWriter = new(128);
    private static byte[] _reusableBuffer = new byte[128];

    public static void Register(World world)
    {
        world.System<Position, Velocity, PreviousVelocity, FacingDirection, NetworkSequence, NetworkId>("NetworkBroadcastSystem")
            .With<LocalPlayerTag>()
            .Kind(Ecs.PostUpdate)
            // ARCHITECTURE FIX: Inject the Iter 'it' object to get true DeltaTime
            .Each((Iter it, int i, ref Position pos, ref Velocity velocity, ref PreviousVelocity prevVelocity, ref FacingDirection facing, ref NetworkSequence seq, ref NetworkId netId) =>
            {
                if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

                // Safely tracks time across OS / FPS unlocks
                seq.TimeSinceLastPacket += it.DeltaTime();

                bool velocityChanged = MathF.Abs(velocity.X - prevVelocity.X) > 0.01f ||
                                       MathF.Abs(velocity.Y - prevVelocity.Y) > 0.01f;

                if (velocityChanged || seq.TimeSinceLastPacket >= 0.05f)
                {
                    seq.LatestSequence++;
                    seq.TimeSinceLastPacket = 0f;

                    var packet = new PlayerTransformPacket
                    {
                        SequenceNumber = seq.LatestSequence,
                        X = pos.X,
                        Y = pos.Y,
                        Vx = velocity.X,
                        Vy = velocity.Y,
                        FacingDirection = facing.Value,
                        EntityNetworkSequenceId = netId.Value
                    };

                    _bufferWriter.Clear();
                    var headerSpan = _bufferWriter.GetSpan(1);
                    headerSpan[0] = PacketTypes.Transform;
                    _bufferWriter.Advance(1);

                    MemoryPackSerializer.Serialize(_bufferWriter, packet);
                    int totalLength = _bufferWriter.WrittenCount;

                    if (_reusableBuffer.Length < totalLength)
                    {
                        Array.Resize(ref _reusableBuffer, totalLength * 2);
                    }
                    _bufferWriter.WrittenSpan.CopyTo(_reusableBuffer);

                    foreach (var member in SteamManager.CurrentLobby.Value.Members)
                    {
                        if (member.Id != SteamClient.SteamId)
                        {
                            SteamNetworking.SendP2PPacket(member.Id, _reusableBuffer, totalLength, 0, P2PSend.Unreliable);
                        }
                    }

                    prevVelocity.X = velocity.X;
                    prevVelocity.Y = velocity.Y;
                }
            });
    }
}
