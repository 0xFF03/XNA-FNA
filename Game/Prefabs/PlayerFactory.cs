using Flecs.NET.Core;
using Steamworks;
using nkast.Aether.Physics2D.Dynamics;
using MyGame.Engine.Networking;
using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;
using MyGame.Game.Renderers;
using MyGame.Game.Environment;

using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Prefabs;

public static class PhysicsLayers
{
    public const Category Environment = Category.Cat1;
    public const Category LocalPlayer = Category.Cat2;
    public const Category RemotePlayer = Category.Cat3;
    public const Category EnemyAndProjectiles = Category.Cat4;
}

public static class PlayerFactory
{
    public const float PixelsPerMeter = 32f;

    public static Entity CreateLocal(Flecs.NET.Core.World world, int classId, ulong uniqueId, float spawnX, float spawnY)
    {
        string entityLookupName = $"p_{uniqueId}";
        var initialPosition = new AetherVector2(spawnX / PixelsPerMeter, spawnY / PixelsPerMeter);
        var aetherBody = Game1.Instance.PhysicsWorld.CreateCapsule(14f / PixelsPerMeter, 5f / PixelsPerMeter, 1f, initialPosition, 0f, BodyType.Dynamic);

        aetherBody.FixedRotation = true;
        aetherBody.IsBullet = true;
        aetherBody.Tag = uniqueId;

        if (aetherBody.FixtureList.Count > 0)
        {
           aetherBody.FixtureList[0].CollisionCategories = PhysicsLayers.LocalPlayer;
           aetherBody.FixtureList[0].CollidesWith = PhysicsLayers.Environment | PhysicsLayers.EnemyAndProjectiles | PhysicsLayers.RemotePlayer;
        }

        Entity e = world.Entity(entityLookupName)
           .Add<LocalPlayerTag>()
           .Add<MatchEntityTag>()
           .Set(new LocalInput { AxisX = 0, AxisY = 0, JumpJustPressed = false })
           .Set(new GroundState { IsGrounded = false, CoyoteTimer = 0f })
           .Set(new Position { X = spawnX, Y = spawnY })
           .Set(new PreviousPosition { X = spawnX, Y = spawnY })
           .Set(new Velocity { X = 0, Y = 0 })
           .Set(new PreviousVelocity { X = 0, Y = 0 })
           .Set(new CharacterClass { Id = classId })
           .Set(new NetworkOwner { Value = SteamClient.SteamId })
           .Set(new NetworkId { Value = uniqueId })
           .Set(new NetworkSequence { LatestSequence = 0, TimeSinceLastPacket = 0f })
           .Set(new FacingDirection { Value = 1 })
           .Set(new Health { Current = 100, Max = 100 })
           .Set(new PhysicsBody { Value = aetherBody });

        NetworkRegistry.Add(uniqueId, e);
        return e;
    }

    public static Entity CreateRemote(Flecs.NET.Core.World world, string entityKey, PlayerTransformPacket packet, SteamId senderId)
    {
        var startPos = new AetherVector2(packet.X / PixelsPerMeter, packet.Y / PixelsPerMeter);
        var aetherBody = Game1.Instance.PhysicsWorld.CreateCapsule(14f / PixelsPerMeter, 5f / PixelsPerMeter, 1f, startPos, 0f, BodyType.Kinematic);

        aetherBody.Tag = packet.EntityNetworkSequenceId;

        Entity e = world.Entity(entityKey)
            .Add<RemotePlayerTag>()
            .Add<MatchEntityTag>()
            .Set(new Position { X = packet.X, Y = packet.Y })
            .Set(new PreviousPosition { X = packet.X, Y = packet.Y })
            .Set(new TargetPosition { X = packet.X, Y = packet.Y })
            .Set(new Velocity { X = packet.Vx, Y = packet.Vy })
            .Set(new CharacterClass { Id = packet.CharacterClassId })
            .Set(new NetworkOwner { Value = senderId })
            .Set(new NetworkId { Value = packet.EntityNetworkSequenceId })
            .Set(new NetworkSequence { LatestSequence = packet.SequenceNumber, TimeSinceLastPacket = 0f })
            .Set(new FacingDirection { Value = packet.FacingDirection })
            .Set(new Health { Current = 100, Max = 100 })
            .Set(new PhysicsBody { Value = aetherBody });

        NetworkRegistry.Add(packet.EntityNetworkSequenceId, e);
        return e;
    }
}
