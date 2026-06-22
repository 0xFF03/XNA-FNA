using System;
using System.Buffers;
using Flecs.NET.Core;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.Platform;
using Steamworks;
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
                // ARCHITECTURE FIX: Call the newly generalized Engine-level ID generator
                ulong netId = NetworkIdGenerator.GetNextNetworkId();

                ulong ownerId = SteamManager.IsSteamActive ? SteamClient.SteamId.Value : 0;
                SteamId safeOwnerId = SteamManager.IsSteamActive ? SteamClient.SteamId : (SteamId)0;

                ProjectileFactory.Create(it.World(), req.StartX, req.StartY, req.VelocityX, req.VelocityY, netId, safeOwnerId, req.TargetPhysicsWorld);

                if (SteamManager.IsSteamActive && SteamManager.CurrentLobby.HasValue)
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

                    foreach (var member in SteamManager.CurrentLobby.Value.Members)
                    {
                        if (member.Id.Value != ownerId)
                        {
                            SteamNetworking.SendP2PPacket(member.Id, _reusableBuffer, totalLength, 1, P2PSend.Reliable);
                        }
                    }
                }

                it.Entity(i).Destruct();
            });

        world.System<PhysicsComponents.PhysicsBody, Position, BaseCombatComponents.Lifetime>("ProjectileUpdateSystem")
            .Kind(Ecs.PreUpdate)
            .With<BaseCombatComponents.ProjectileTag>()
            .Each((Iter it, int i, ref PhysicsComponents.PhysicsBody pBody, ref Position pos, ref BaseCombatComponents.Lifetime life) =>
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
                    var body = pBody.Value;
                    pos.X = body.Position.X * PhysicsSettings.PixelsPerMeter;
                    pos.Y = body.Position.Y * PhysicsSettings.PixelsPerMeter;
                }
            });
    }
}
