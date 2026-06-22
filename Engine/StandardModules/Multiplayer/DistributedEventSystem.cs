using System;
using System.Buffers;
using Flecs.NET.Core;
using Steamworks;
using MemoryPack;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Combat;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class DistributedEventSystem
{
    [ThreadStatic] private static ArrayBufferWriter<byte>? _bufferWriter;
    [ThreadStatic] private static byte[]? _reusableBuffer;

    public static void Register(World world)
    {
        world.System<BaseCombatComponents.Health>("DeathSystem")
            .Kind(Ecs.PostUpdate)
            .Without<BaseCombatComponents.DeadTag>()
            .Each((Entity e, ref BaseCombatComponents.Health health) =>
            {
                if (health.Current <= 0)
                {
                    e.Add<BaseCombatComponents.DeadTag>();
                    e.Destruct();
                }
            });
    }

    public static void BroadcastAndApplyEvent(ulong targetNetId, byte eventType, int intPayload = 0, float floatPayload = 0f)
    {
        ApplyEventLocally(targetNetId, eventType, intPayload, floatPayload);

        if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

        var packet = new DistributedEventPacket
        {
            TargetNetworkId = targetNetId,
            EventType = eventType,
            IntPayload = intPayload,
            FloatPayload = floatPayload
        };

        _bufferWriter ??= new ArrayBufferWriter<byte>(128);
        _reusableBuffer ??= new byte[128];

        _bufferWriter.Clear();
        var headerSpan = _bufferWriter.GetSpan(1);
        headerSpan[0] = PacketTypes.DistributedEvent;
        _bufferWriter.Advance(1);

        MemoryPackSerializer.Serialize(_bufferWriter, packet);
        int packetLength = _bufferWriter.WrittenCount;

        if (_reusableBuffer.Length < packetLength) Array.Resize(ref _reusableBuffer, packetLength * 2);
        _bufferWriter.WrittenSpan.CopyTo(_reusableBuffer);

        foreach (var member in SteamManager.CurrentLobby.Value.Members)
        {
            if (member.Id != SteamClient.SteamId)
            {
                SteamNetworking.SendP2PPacket(member.Id, _reusableBuffer, packetLength, 1, P2PSend.Reliable);
            }
        }
    }

    private static void ApplyEventLocally(ulong targetNetId, byte eventType, int intPayload, float floatPayload)
    {
        Entity? target = NetworkRegistry.GetEntity(targetNetId);
        if (target.HasValue && target.Value.IsAlive())
        {
            Entity entity = target.Value;

            if (eventType == (byte)GameEventType.Despawn)
            {
                entity.Destruct();
                return;
            }

            if (eventType == (byte)GameEventType.Damage && entity.Has<BaseCombatComponents.Health>())
            {
                ref var health = ref entity.GetMut<BaseCombatComponents.Health>();
                health.Current -= intPayload;
            }
        }
    }
}
