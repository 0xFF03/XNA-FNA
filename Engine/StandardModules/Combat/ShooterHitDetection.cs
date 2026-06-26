using Flecs.NET.Core;
using nkast.Aether.Physics2D.Dynamics.Contacts;
using MyGame.Engine.Platform.Networking;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Combat;

public static class ShooterHitDetection
{
    public static void Register(Flecs.NET.Core.World world)
    {
        world.System<PhysicsComponents.PhysicsBody, BaseCombatComponents.Damage, BaseCombatComponents.CombatAlignment, NetworkOwner>("LocalHitDetectionSystem")
            .Kind(Ecs.PostUpdate)
            .With<BaseCombatComponents.ProjectileTag>()
            .Each((Iter it, int row, ref PhysicsComponents.PhysicsBody pBody, ref BaseCombatComponents.Damage dmg, ref BaseCombatComponents.CombatAlignment projAlign, ref NetworkOwner projOwner) =>
            {
                if (pBody.Value == null) return;

                var net = NetworkServiceLocator.Provider;
                Entity projectileEntity = it.Entity(row);
                ulong projNetId = projectileEntity.Has<NetworkId>() ? projectileEntity.Get<NetworkId>().Value : 0;
                bool iOwnProjectile = !net.IsActive || projOwner.Value == net.LocalUserId;

                ContactEdge ce = pBody.Value.ContactList;
                while (ce != null)
                {
                    if (ce.Contact.IsTouching)
                    {
                        var otherBody = ce.Contact.FixtureA.Body == pBody.Value ? ce.Contact.FixtureB.Body : ce.Contact.FixtureA.Body;
                        ulong victimNetId = otherBody.Tag is ulong id ? id : 0;

                        Entity victimEntity = NetworkRegistry.GetEntity(victimNetId);

                        if (victimEntity.Id != 0 && victimEntity.Has<BaseCombatComponents.CombatAlignment>())
                        {
                            var victimAlign = victimEntity.Get<BaseCombatComponents.CombatAlignment>();

                            if (victimAlign.Value == projAlign.Value)
                            {
                                ce = ce.Next;
                                continue;
                            }
                        }

                        bool victimIsPlayer = victimEntity.Id != 0 && (victimEntity.Has<LocalPlayerTag>() || victimEntity.Has<RemotePlayerTag>());
                        bool iOwnVictim = victimEntity.Id != 0 && victimEntity.Has<NetworkOwner>() && victimEntity.Get<NetworkOwner>().Value == net.LocalUserId;

                        bool processCollisionLocally = false;

                        if (victimIsPlayer && iOwnVictim)
                        {
                            processCollisionLocally = true;
                        }
                        else if (!victimIsPlayer && iOwnProjectile)
                        {
                            processCollisionLocally = true;
                        }

                        if (processCollisionLocally)
                        {
                            if (victimNetId != 0) DistributedEventSystem.BroadcastAndApplyEvent(victimNetId, (byte)GameEventType.Damage, dmg.Amount);

                            if (projNetId != 0) DistributedEventSystem.BroadcastAndApplyEvent(projNetId, (byte)GameEventType.Despawn);
                            else projectileEntity.Destruct();

                            return;
                        }
                    }
                    ce = ce.Next;
                }
            });
    }
}
