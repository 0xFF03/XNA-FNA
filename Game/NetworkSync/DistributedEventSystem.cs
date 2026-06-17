using System;
using System.Buffers;
using Flecs.NET.Core;
using Steamworks;
using MemoryPack;
using MyGame.Engine.Networking;

using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;

namespace MyGame.Game.NetworkSync;

public static class DistributedEventSystem
{
    private static readonly ArrayBufferWriter<byte> _bufferWriter = new(128);
    private static byte[] _reusableBuffer = new byte[128];

    public static void Register(World world)
    {
        world.Observer<OutboundDistributedEvent>("ProcessLocalEventObserver")
            .Event(Ecs.OnSet)
            .Each((Iter it, int i, ref OutboundDistributedEvent syncEvent) =>
            {
                Entity reqEntity = it.Entity(i);

                ApplyEventLocally(syncEvent.TargetNetworkId, syncEvent);

                if (SteamManager.IsSteamActive && SteamManager.CurrentLobby.HasValue)
                {
                    var packet = new DistributedEventPacket
                    {
                        InstigatorNetworkId = SteamClient.SteamId.Value,
                        TargetNetworkId = syncEvent.TargetNetworkId,
                        EventType = syncEvent.EventType,
                        IntPayload = syncEvent.IntPayload,
                        FloatPayload = syncEvent.FloatPayload
                    };

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

                reqEntity.Destruct();
            });

        world.System<Health>("DeathSystem")
            .Kind(Ecs.PostUpdate)
            .Without<DeadTag>()
            .Each((Entity e, ref Health health) =>
            {
                if (health.Current <= 0)
                {
                    e.Add<DeadTag>();
                    e.Destruct();
                }
            });
    }

    private static void ApplyEventLocally(ulong targetNetId, OutboundDistributedEvent ev)
    {
        Entity? target = NetworkRegistry.GetEntity(targetNetId);
        if (target.HasValue && target.Value.IsAlive())
        {
            Entity entity = target.Value;

            if (ev.EventType == (byte)GameEventType.Despawn)
            {
                entity.Destruct();
                return;
            }

            if (ev.EventType == (byte)GameEventType.Damage && entity.Has<Health>())
            {
                ref var health = ref entity.GetMut<Health>();
                health.Current -= ev.IntPayload;
            }
        }
    }
}
