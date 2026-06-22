using System;
using System.Buffers;
using Flecs.NET.Core;
using Steamworks;
using MemoryPack;
using MyGame.Engine.Platform;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class NetworkBroadcastSystem
{
    [ThreadStatic] private static ArrayBufferWriter<byte>? _bufferWriter;
    [ThreadStatic] private static byte[]? _reusableBuffer;

    public static void Register(World world)
    {
        // ARCHITECTURE FIX: Query now includes NetworkOwner and drops LocalPlayerTag restriction
        world.System<Position, Velocity, PreviousVelocity, FacingDirection, NetworkSequence, NetworkId, PhysicsDimension, NetworkOwner>("NetworkBroadcastSystem")
            .Kind(Ecs.PostUpdate)
            .Each((Iter it, int i, ref Position pos, ref Velocity velocity, ref PreviousVelocity prevVelocity, ref FacingDirection facing, ref NetworkSequence seq, ref NetworkId netId, ref PhysicsDimension dimension, ref NetworkOwner owner) =>
            {
                if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

                // Only broadcast entities we have explicit authority over (Local Player, Controlled Vehicles)
                if (owner.Value != SteamClient.SteamId) return;

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
                        EntityNetworkSequenceId = netId.Value,
                        TargetPhysicsWorld = dimension.Name
                    };

                    _bufferWriter ??= new ArrayBufferWriter<byte>(128);
                    _reusableBuffer ??= new byte[128];

                    _bufferWriter.Clear();
                    var headerSpan = _bufferWriter.GetSpan(1);
                    headerSpan[0] = PacketTypes.Transform;
                    _bufferWriter.Advance(1);

                    MemoryPackSerializer.Serialize(_bufferWriter, packet);
                    int totalLength = _bufferWriter.WrittenCount;

                    if (_reusableBuffer.Length < totalLength) Array.Resize(ref _reusableBuffer, totalLength * 2);
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
