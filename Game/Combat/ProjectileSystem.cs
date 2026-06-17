using System;
using System.Buffers;
using Flecs.NET.Core;
using MyGame.Engine.Networking;
using Steamworks;
using MemoryPack;

using MyGame.Game.Core;
using MyGame.Game.Physics;
using MyGame.Prefabs;

namespace MyGame.Game.Combat;

public static class ProjectileSystem
{
    private static readonly ArrayBufferWriter<byte> _bufferWriter = new(128);
    private static byte[] _reusableBuffer = new byte[128];

    public static void Register(World world)
    {
        world.Observer<ProjectileSpawnRequest>("ProjectileSpawnObserver")
            .Event(Ecs.OnSet)
            .Each((Iter it, int i, ref ProjectileSpawnRequest req) =>
            {
                ulong netId = NetworkIdGenerator.GetNextProjectileId();

                ProjectileFactory.Create(it.World(), req.StartX, req.StartY, req.VelocityX, req.VelocityY, netId, SteamClient.SteamId);

                if (SteamManager.IsSteamActive && SteamManager.CurrentLobby.HasValue)
                {
                    var packet = new ProjectileSpawnPacket
                    {
                        StartX = req.StartX,
                        StartY = req.StartY,
                        VelocityX = req.VelocityX,
                        VelocityY = req.VelocityY,
                        EntityNetworkSequenceId = netId,
                        OwnerSteamId = SteamClient.SteamId.Value
                    };

                    _bufferWriter.Clear();
                    var headerSpan = _bufferWriter.GetSpan(1);
                    headerSpan[0] = PacketTypes.ProjectileSpawn;
                    _bufferWriter.Advance(1);

                    MemoryPackSerializer.Serialize(_bufferWriter, packet);
                    int totalLength = _bufferWriter.WrittenCount;

                    if (_reusableBuffer.Length < totalLength) Array.Resize(ref _reusableBuffer, totalLength * 2);
                    _bufferWriter.WrittenSpan.CopyTo(_reusableBuffer);

                    foreach (var member in SteamManager.CurrentLobby.Value.Members)
                    {
                        if (member.Id != SteamClient.SteamId)
                        {
                            SteamNetworking.SendP2PPacket(member.Id, _reusableBuffer, totalLength, 1, P2PSend.Reliable);
                        }
                    }
                }

                it.Entity(i).Destruct();
            });

        world.System<PhysicsBody, Position, Lifetime>("ProjectileUpdateSystem")
            .Kind(Ecs.PreUpdate)
            .With<ProjectileTag>()
            // ARCHITECTURE FIX: Inject Iter to use safe DeltaTime for lifespans
            .Each((Iter it, int i, ref PhysicsBody pBody, ref Position pos, ref Lifetime life) =>
            {
                Entity e = it.Entity(i);

                life.Remaining -= it.DeltaTime();

                if (life.Remaining <= 0)
                {
                    e.Destruct();
                    return;
                }

                if (pBody.Value != null)
                {
                    var body = (nkast.Aether.Physics2D.Dynamics.Body)pBody.Value;
                    pos.X = body.Position.X * PlayerFactory.PixelsPerMeter;
                    pos.Y = body.Position.Y * PlayerFactory.PixelsPerMeter;
                }
            });
    }
}
