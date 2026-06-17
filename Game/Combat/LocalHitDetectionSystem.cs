using Flecs.NET.Core;
using Steamworks;
using MyGame.Engine.Networking;
using nkast.Aether.Physics2D.Dynamics.Contacts;

using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;

namespace MyGame.Game.Combat;

public static class LocalHitDetectionSystem
{
    public static void Register(World world)
    {
        world.System<PhysicsBody, Damage, NetworkOwner>("LocalHitDetectionSystem")
            .Kind(Ecs.PostUpdate)
            .With<ProjectileTag>()
            .Each((Iter it, int row, ref PhysicsBody pBody, ref Damage dmg, ref NetworkOwner owner) =>
            {
                if (pBody.Value == null) return;

                Entity projectileEntity = it.Entity(row);
                bool isMyProjectile = owner.Value == SteamClient.SteamId;

                ContactEdge ce = pBody.Value.ContactList;
                while (ce != null)
                {
                    if (ce.Contact.IsTouching)
                    {
                        var otherBody = ce.Contact.FixtureA.Body == pBody.Value ? ce.Contact.FixtureB.Body : ce.Contact.FixtureA.Body;

                        if (otherBody.Tag == null)
                        {
                            if (isMyProjectile && projectileEntity.Has<NetworkId>())
                            {
                                ulong projNetId = projectileEntity.Get<NetworkId>().Value;
                                it.World().Entity().Set(new OutboundDistributedEvent
                                {
                                    TargetNetworkId = projNetId,
                                    EventType = (byte)GameEventType.Despawn
                                });
                            }
                            projectileEntity.Destruct();
                            return;
                        }

                        if (otherBody.Tag is ulong victimNetId)
                        {
                            Entity? target = NetworkRegistry.GetEntity(victimNetId);
                            if (target == null) return;

                            bool hitLocalPlayer = target.Value.Has<LocalPlayerTag>();
                            bool hitRemotePlayer = target.Value.Has<RemotePlayerTag>();

                            if (hitLocalPlayer)
                            {
                                it.World().Entity().Set(new OutboundDistributedEvent
                                {
                                    TargetNetworkId = victimNetId,
                                    EventType = (byte)GameEventType.Damage,
                                    IntPayload = dmg.Amount
                                });

                                if (isMyProjectile && projectileEntity.Has<NetworkId>())
                                {
                                    ulong projNetId = projectileEntity.Get<NetworkId>().Value;
                                    it.World().Entity().Set(new OutboundDistributedEvent
                                    {
                                        TargetNetworkId = projNetId,
                                        EventType = (byte)GameEventType.Despawn
                                    });
                                }
                                projectileEntity.Destruct();
                                return;
                            }

                            if (hitRemotePlayer)
                            {
                                if (isMyProjectile && projectileEntity.Has<NetworkId>())
                                {
                                    ulong projNetId = projectileEntity.Get<NetworkId>().Value;
                                    it.World().Entity().Set(new OutboundDistributedEvent
                                    {
                                        TargetNetworkId = projNetId,
                                        EventType = (byte)GameEventType.Despawn
                                    });
                                }
                                projectileEntity.Destruct();
                                return;
                            }

                            if (isMyProjectile && !hitLocalPlayer && !hitRemotePlayer)
                            {
                                it.World().Entity().Set(new OutboundDistributedEvent
                                {
                                    TargetNetworkId = victimNetId,
                                    EventType = (byte)GameEventType.Damage,
                                    IntPayload = dmg.Amount
                                });

                                if (projectileEntity.Has<NetworkId>())
                                {
                                    ulong projNetId = projectileEntity.Get<NetworkId>().Value;
                                    it.World().Entity().Set(new OutboundDistributedEvent
                                    {
                                        TargetNetworkId = projNetId,
                                        EventType = (byte)GameEventType.Despawn
                                    });
                                }
                                projectileEntity.Destruct();
                                return;
                            }
                        }
                    }
                    ce = ce.Next;
                }
            });
    }
}
