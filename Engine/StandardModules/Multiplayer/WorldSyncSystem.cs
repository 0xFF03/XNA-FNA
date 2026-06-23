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
                TargetPhysicsWorld = dimension.Name
            });
        });

        var marksQuery = world.QueryBuilder<Position, WorldMark>().Build();
        marksQuery.Each((Entity e, ref Position pos, ref WorldMark mark) =>
        {
            // ARCHITECTURE FIX: Flawless Spatial Catch-Up.
            // Transmits actual X and Y coordinates. Interaction state is safely packed into Health.
            snapshots.Add(new EntitySnapshot
            {
                NetworkId = e.Id,
                EntityType = 255,
                X = pos.X,
                Y = pos.Y,
                Health = mark.InteractionState,
                FacingDirection = 0,
                OwnerSteamId = 0,
                TargetPhysicsWorld = mark.UniqueMarkId
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
        _bufferWriter.WrittenSpan.CopyTo(_reusableBuffer);

        SteamNetworking.SendP2PPacket(newPlayerId, _reusableBuffer, _bufferWriter.WrittenCount, 1, P2PSend.Reliable);
    }
}
