using Flecs.NET.Core;
using Steamworks;
using nkast.Aether.Physics2D.Dynamics;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.Core;
using MyGame.Game.Core;

using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Prefabs;

public static class ProjectileFactory
{
    public static Entity Create(Flecs.NET.Core.World world, float startX, float startY, float velX, float velY, ulong netId, SteamId ownerId, string targetPhysicsWorld = "MacroSpace")
    {
        string entityKey = $"proj_{netId}";

        float physX = startX / PhysicsSettings.PixelsPerMeter;
        float physY = startY / PhysicsSettings.PixelsPerMeter;
        float physRadius = 4f / PhysicsSettings.PixelsPerMeter;

        var physicsWorld = PhysicsWorldManager.GetWorld(targetPhysicsWorld);
        var aetherBody = physicsWorld.CreateCircle(physRadius, 1f, new AetherVector2(physX, physY), BodyType.Dynamic);

        aetherBody.IgnoreGravity = true;
        aetherBody.LinearVelocity = new AetherVector2(velX / PhysicsSettings.PixelsPerMeter, velY / PhysicsSettings.PixelsPerMeter);
        aetherBody.IsBullet = true;
        aetherBody.Tag = netId;

        foreach (var fixture in aetherBody.FixtureList)
        {
            fixture.IsSensor = true;
            fixture.CollisionCategories = PhysicsLayers.EnemyAndProjectiles;
            fixture.CollidesWith = PhysicsLayers.Environment | PhysicsLayers.RemotePlayer | PhysicsLayers.LocalPlayer;
        }

        Entity e = world.Entity(entityKey)
            .Add<BaseCombatComponents.ProjectileTag>()
            .Add<MatchEntityTag>()
            .Set(new Position { X = startX, Y = startY })
            .Set(new Velocity { X = velX, Y = velY })
            .Set(new PreviousPosition { X = startX, Y = startY })
            .Set(new BaseCombatComponents.Lifetime { Remaining = 5f })
            .Set(new BaseCombatComponents.Damage { Amount = 10 })
            .Set(new NetworkId { Value = netId })
            .Set(new NetworkOwner { Value = ownerId })
            // ARCHITECTURE FIX: Fired by players, so it is inherently friendly.
            .Set(new BaseCombatComponents.CombatAlignment { Value = BaseCombatComponents.Alignment.Friendly })
            .Set(new PhysicsComponents.PhysicsBody { Value = aetherBody });

        NetworkRegistry.Add(netId, e);
        return e;
    }
}
