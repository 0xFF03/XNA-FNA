using System;
using System.Buffers;
using Flecs.NET.Core;
using Steamworks;
using MemoryPack;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class NetworkBroadcastSystem
{
    [ThreadStatic] private static ArrayBufferWriter<byte>? _bufferWriter;
    [ThreadStatic] private static byte[]? _reusableBuffer;

    public static void Register(World world)
    {
        world.System<Position, Velocity, PreviousVelocity, FacingDirection, NetworkSequence, NetworkId, PhysicsDimension, NetworkOwner>("NetworkBroadcastSystem")
            .Kind(Ecs.PostUpdate)
            .Each((Iter it, int i, ref Position pos, ref Velocity velocity, ref PreviousVelocity prevVelocity, ref FacingDirection facing, ref NetworkSequence seq, ref NetworkId netId, ref PhysicsDimension dimension, ref NetworkOwner owner) =>
            {
                if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

                if (owner.Value != SteamClient.SteamId) return;

                // ARCHITECTURE FIX: Never broadcast logic for entities residing in sleeping dimensions.
                if (!PhysicsWorldManager.ActiveDimensions.Contains(dimension.Name)) return;

                seq.TimeSinceLastPacket += it.DeltaTime();

                bool velocityChanged = MathF.Abs(velocity.X - prevVelocity.X) > 0.01f ||
                                       MathF.Abs(velocity.Y - prevVelocity.Y) > 0.01f;

                bool isMoving = MathF.Abs(velocity.X) > 0.01f || MathF.Abs(velocity.Y) > 0.01f;

                float broadcastInterval = isMoving ? 0.05f : 1.0f;

                if (velocityChanged || seq.TimeSinceLastPacket >= broadcastInterval)
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

                    ulong localId = SteamClient.SteamId.Value;
                    foreach (var memberId in SteamManager.ActiveLobbyMembers)
                    {
                        if (memberId != localId)
                        {
                            SteamNetworking.SendP2PPacket(memberId, _reusableBuffer, totalLength, 0, P2PSend.Unreliable);
                        }
                    }

                    prevVelocity.X = velocity.X;
                    prevVelocity.Y = velocity.Y;
                }
            });
    }
}
