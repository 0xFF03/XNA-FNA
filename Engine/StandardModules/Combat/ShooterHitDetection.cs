using Flecs.NET.Core;
using Steamworks;
using nkast.Aether.Physics2D.Dynamics.Contacts;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Combat;

public static class ShooterHitDetection
{
    public static void Register(Flecs.NET.Core.World world)
    {
        world.System<PhysicsComponents.PhysicsBody, BaseCombatComponents.Damage, NetworkOwner>("LocalHitDetectionSystem")
            .Kind(Ecs.PostUpdate)
            .With<BaseCombatComponents.ProjectileTag>()
            .Each((Iter it, int row, ref PhysicsComponents.PhysicsBody pBody, ref BaseCombatComponents.Damage dmg, ref NetworkOwner projOwner) =>
            {
                if (pBody.Value == null) return;

                Entity projectileEntity = it.Entity(row);
                ulong projNetId = projectileEntity.Has<NetworkId>() ? projectileEntity.Get<NetworkId>().Value : 0;
                bool iOwnProjectile = !SteamManager.IsSteamActive || projOwner.Value == SteamClient.SteamId;

                ContactEdge ce = pBody.Value.ContactList;
                while (ce != null)
                {
                    if (ce.Contact.IsTouching)
                    {
                        var otherBody = ce.Contact.FixtureA.Body == pBody.Value ? ce.Contact.FixtureB.Body : ce.Contact.FixtureA.Body;
                        ulong victimNetId = otherBody.Tag is ulong id ? id : 0;

                        Entity victimEntity = NetworkRegistry.GetEntity(victimNetId);

                        bool victimIsPlayer = victimEntity.Id != 0 && (victimEntity.Has<LocalPlayerTag>() || victimEntity.Has<RemotePlayerTag>());
                        bool iOwnVictim = victimEntity.Id != 0 && victimEntity.Has<NetworkOwner>() && victimEntity.Get<NetworkOwner>().Value == SteamClient.SteamId;

                        // ARCHITECTURE FIX: True "Local Screen is God" processing logic.
                        bool processCollisionLocally = false;

                        if (victimIsPlayer && iOwnVictim)
                        {
                            // A bullet hit MY body on MY screen. Favour the Dodger.
                            processCollisionLocally = true;
                        }
                        else if (!victimIsPlayer && iOwnProjectile)
                        {
                            // MY bullet hit a monster/environment on MY screen. Favour the Shooter.
                            processCollisionLocally = true;
                        }

                        if (processCollisionLocally)
                        {
                            if (victimNetId != 0) DistributedEventSystem.BroadcastAndApplyEvent(victimNetId, (byte)GameEventType.Damage, dmg.Amount);

                            if (projNetId != 0) DistributedEventSystem.BroadcastAndApplyEvent(projNetId, (byte)GameEventType.Despawn);
                            else projectileEntity.Destruct();

                            return; // Target hit, immediately escape to prevent multi-contact shredding
                        }
                    }
                    ce = ce.Next;
                }
            });
    }
}
