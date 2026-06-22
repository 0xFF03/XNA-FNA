using System;
using System.Buffers;
using System.Collections.Generic;
using Flecs.NET.Core;
using Steamworks;
using MemoryPack;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class WorldSyncSystem
{
    [ThreadStatic] private static ArrayBufferWriter<byte>? _bufferWriter;
    [ThreadStatic] private static byte[]? _reusableBuffer;

    public static void SendWorldSnapshot(World world, SteamId newPlayerId)
    {
        if (!SteamManager.KnownHostId.HasValue || SteamManager.KnownHostId.Value != SteamClient.SteamId) return;

        var snapshots = new List<EntitySnapshot>();

        // ARCHITECTURE FIX: Query explicitly demands the PhysicsDimension
        var syncQuery = world.QueryBuilder<Position, NetworkId, NetworkOwner, PhysicsDimension>().Build();

        syncQuery.Each((Entity e, ref Position pos, ref NetworkId netId, ref NetworkOwner owner, ref PhysicsDimension dimension) =>
        {
            if (owner.Value == newPlayerId) return;

            byte type = 0;
            int hp = e.Has<BaseCombatComponents.Health>() ? e.Get<BaseCombatComponents.Health>().Current : 100;
            int dir = e.Has<FacingDirection>() ? e.Get<FacingDirection>().Value : 1;

            snapshots.Add(new EntitySnapshot
            {
                NetworkId = netId.Value,
                EntityType = type,
                X = pos.X,
                Y = pos.Y,
                Health = hp,
                FacingDirection = dir,
                OwnerSteamId = owner.Value.Value,
                TargetPhysicsWorld = dimension.Name // ARCHITECTURE FIX: Synchronize the dimension across the network
            });
        });

        if (snapshots.Count == 0) return;

        var packet = new WorldStatePacket { Entities = snapshots.ToArray() };

        _bufferWriter ??= new ArrayBufferWriter<byte>(1024 * 64);
        _reusableBuffer ??= new byte[1024 * 64];

        _bufferWriter.Clear();
        var headerSpan = _bufferWriter.GetSpan(1);
        headerSpan[0] = PacketTypes.WorldStateSnapshot;
        _bufferWriter.Advance(1);

        MemoryPackSerializer.Serialize(_bufferWriter, packet);
        int packetLength = _bufferWriter.WrittenCount;

        if (_reusableBuffer.Length < packetLength) Array.Resize(ref _reusableBuffer, packetLength * 2);
        _bufferWriter.WrittenSpan.CopyTo(_reusableBuffer);

        SteamNetworking.SendP2PPacket(newPlayerId, _reusableBuffer, packetLength, 1, P2PSend.Reliable);
    }
}
