using Flecs.NET.Core;
using Steamworks;
using nkast.Aether.Physics2D.Dynamics;
using MyGame.Engine.Networking;

// Universal Headers
using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;
using MyGame.Game.Renderers;
using MyGame.Game.Environment;

using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Prefabs;

public static class ProjectileFactory
{
    public static Entity Create(Flecs.NET.Core.World world, float startX, float startY, float velX, float velY, ulong netId, SteamId ownerId)
    {
        string entityKey = $"proj_{netId}";

        float physX = startX / PlayerFactory.PixelsPerMeter;
        float physY = startY / PlayerFactory.PixelsPerMeter;
        float physRadius = 4f / PlayerFactory.PixelsPerMeter;

        var aetherBody = Game1.Instance.PhysicsWorld.CreateCircle(physRadius, 1f, new AetherVector2(physX, physY), BodyType.Dynamic);
        aetherBody.IgnoreGravity = true;
        aetherBody.LinearVelocity = new AetherVector2(velX / PlayerFactory.PixelsPerMeter, velY / PlayerFactory.PixelsPerMeter);
        aetherBody.IsBullet = true;
        aetherBody.Tag = netId;

        foreach (var fixture in aetherBody.FixtureList)
        {
            fixture.IsSensor = true;
            fixture.CollisionCategories = PhysicsLayers.EnemyAndProjectiles;
            fixture.CollidesWith = PhysicsLayers.Environment | PhysicsLayers.RemotePlayer | PhysicsLayers.LocalPlayer;
        }

        Entity e = world.Entity(entityKey)
            .Add<ProjectileTag>()
            .Add<MatchEntityTag>()
            .Set(new Position { X = startX, Y = startY })
            .Set(new Velocity { X = velX, Y = velY })
            .Set(new PreviousPosition { X = startX, Y = startY })
            .Set(new Lifetime { Remaining = 5f })
            .Set(new Damage { Amount = 10 })
            .Set(new NetworkId { Value = netId })
            .Set(new NetworkOwner { Value = ownerId })
            .Set(new PhysicsBody { Value = aetherBody });

        NetworkRegistry.Add(netId, e);
        return e;
    }
}
