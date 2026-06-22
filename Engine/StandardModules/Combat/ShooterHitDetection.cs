using Flecs.NET.Core;
using Steamworks;
using nkast.Aether.Physics2D.Dynamics.Contacts;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;

namespace MyGame.Engine.StandardModules.Combat;

public static class ShooterHitDetection
{
    public static void Register(World world)
    {
        world.System<PhysicsComponents.PhysicsBody, BaseCombatComponents.Damage, NetworkOwner>("LocalHitDetectionSystem")
            .Kind(Ecs.PostUpdate)
            .With<BaseCombatComponents.ProjectileTag>()
            .Each((Iter it, int row, ref PhysicsComponents.PhysicsBody pBody, ref BaseCombatComponents.Damage dmg, ref NetworkOwner owner) =>
            {
                if (pBody.Value == null) return;

                Entity projectileEntity = it.Entity(row);
                bool isMyProjectile = !SteamManager.IsSteamActive || owner.Value == SteamClient.SteamId;
                ulong projNetId = projectileEntity.Has<NetworkId>() ? projectileEntity.Get<NetworkId>().Value : 0;

                ContactEdge ce = pBody.Value.ContactList;
                while (ce != null)
                {
                    if (ce.Contact.IsTouching)
                    {
                        var otherBody = ce.Contact.FixtureA.Body == pBody.Value ? ce.Contact.FixtureB.Body : ce.Contact.FixtureA.Body;

                        if (isMyProjectile)
                        {
                            if (otherBody.Tag is ulong victimNetId)
                            {
                                DistributedEventSystem.BroadcastAndApplyEvent(victimNetId, (byte)GameEventType.Damage, dmg.Amount);
                            }

                            if (projNetId != 0)
                                DistributedEventSystem.BroadcastAndApplyEvent(projNetId, (byte)GameEventType.Despawn);
                            else
                                projectileEntity.Destruct();

                            return;
                        }
                        else
                        {
                            projectileEntity.Destruct();
                            return;
                        }
                    }
                    ce = ce.Next;
                }
            });
    }
}
