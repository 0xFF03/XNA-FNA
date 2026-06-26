using System;
using System.Buffers;
using Flecs.NET.Core;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.Platform.Networking;
using MemoryPack;
using MyGame.Prefabs;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Combat;

public static class ProjectileSystem
{
    [ThreadStatic] private static ArrayBufferWriter<byte>? _bufferWriter;
    [ThreadStatic] private static byte[]? _reusableBuffer;

    public static void Register(World world)
    {
        world.Observer<BaseCombatComponents.ProjectileSpawnRequest>("ProjectileSpawnObserver")
            .Event(Ecs.OnSet)
            .Each((Iter it, int i, ref BaseCombatComponents.ProjectileSpawnRequest req) =>
            {
                var net = NetworkServiceLocator.Provider;
                ulong netId = NetworkIdGenerator.GetNextNetworkId();
                ulong ownerId = net.IsActive ? net.LocalUserId : 0;

                ProjectileFactory.Create(it.World(), req.StartX, req.StartY, req.VelocityX, req.VelocityY, netId, ownerId, req.TargetPhysicsWorld);

                if (net.IsActive && net.IsInLobby)
                {
                    var packet = new ProjectileSpawnPacket
                    {
                        StartX = req.StartX,
                        StartY = req.StartY,
                        VelocityX = req.VelocityX,
                        VelocityY = req.VelocityY,
                        EntityNetworkSequenceId = netId,
                        OwnerSteamId = ownerId,
                        TargetPhysicsWorld = req.TargetPhysicsWorld
                    };

                    _bufferWriter ??= new ArrayBufferWriter<byte>(128);
                    _reusableBuffer ??= new byte[128];

                    _bufferWriter.Clear();
                    var headerSpan = _bufferWriter.GetSpan(1);
                    headerSpan[0] = PacketTypes.ProjectileSpawn;
                    _bufferWriter.Advance(1);

                    MemoryPackSerializer.Serialize(_bufferWriter, packet);
                    int totalLength = _bufferWriter.WrittenCount;

                    if (_reusableBuffer.Length < totalLength) Array.Resize(ref _reusableBuffer, totalLength * 2);
                    _bufferWriter.WrittenSpan.CopyTo(_reusableBuffer);

                    net.BroadcastPacket(_reusableBuffer, totalLength, 1, reliable: true);
                }

                it.Entity(i).Destruct();
            });

        world.System<BaseCombatComponents.Lifetime>("ProjectileUpdateSystem")
            .Kind(Ecs.PreUpdate)
            .With<BaseCombatComponents.ProjectileTag>()
            .Each((Iter it, int i, ref BaseCombatComponents.Lifetime life) =>
            {
                Entity e = it.Entity(i);

                // ARCHITECTURE FIX: Replaced hardcoded '1f / 60f' with precise DeltaTime
                life.Remaining -= it.DeltaTime();

                if (life.Remaining <= 0)
                {
                    e.Destruct();
                }
            });
    }
}
