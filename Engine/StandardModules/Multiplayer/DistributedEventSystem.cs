using System;
using System.Buffers;
using Flecs.NET.Core;
using MemoryPack;
using MyGame.Engine.Platform.Networking;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Game.Core;

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

    public static void BroadcastAndApplyEvent(ulong targetNetId, byte eventType, int intPayload = 0, float floatPayload = 0f, ulong ulongPayload = 0)
    {
        ApplyEventLocally(targetNetId, eventType, intPayload, floatPayload, ulongPayload);

        var net = NetworkServiceLocator.Provider;
        if (!net.IsActive || !net.IsInLobby) return;

        var packet = new DistributedEventPacket
        {
            TargetNetworkId = targetNetId,
            EventType = eventType,
            IntPayload = intPayload,
            FloatPayload = floatPayload,
            UlongPayload = ulongPayload
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

        net.BroadcastPacket(_reusableBuffer, packetLength, 1, reliable: true);
    }

    private static void ApplyEventLocally(ulong targetNetId, byte eventType, int intPayload, float floatPayload, ulong ulongPayload)
    {
        Entity target = NetworkRegistry.GetEntity(targetNetId);
        if (target.Id != 0 && target.IsAlive())
        {
            if (eventType == (byte)GameEventType.Despawn)
            {
                target.Destruct();
                return;
            }

            if (eventType == (byte)GameEventType.Damage && target.Has<BaseCombatComponents.Health>())
            {
                ref var health = ref target.GetMut<BaseCombatComponents.Health>();
                health.Current -= intPayload;
            }

            if (eventType == (byte)GameEventType.InteractSwitch && target.Has<WorldMark>())
            {
                ref var mark = ref target.GetMut<WorldMark>();
                mark.InteractionState = intPayload;
                if (intPayload > 0) target.Remove<InteractableTag>();
            }

            if (eventType == (byte)GameEventType.ClaimAuthority && target.Has<NetworkOwner>())
            {
                target.Set(new NetworkOwner { Value = ulongPayload });
            }
        }
    }
}
