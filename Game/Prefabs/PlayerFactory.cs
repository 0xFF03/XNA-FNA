using Flecs.NET.Core;
using Steamworks;
using nkast.Aether.Physics2D.Dynamics;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.Platform;
using MyGame.Engine.Core;
using MyGame.Game.Core;
using MyGame.Game.Registries;

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
    private static bool CheckIfMapIsTopDown(Flecs.NET.Core.World world)
    {
        bool isTopDown = false;
        world.QueryBuilder<MapComponents.MapInstance>().Build().Each((ref MapComponents.MapInstance map) => { isTopDown = map.Data.IsTopDown; });
        return isTopDown;
    }

    public static Entity CreateLocal(Flecs.NET.Core.World world, int classId, ulong uniqueId, float spawnX, float spawnY, string targetPhysicsWorld = "MacroSpace")
    {
        string entityLookupName = $"p_{uniqueId}";
        var initialPosition = new AetherVector2(spawnX / PhysicsSettings.PixelsPerMeter, spawnY / PhysicsSettings.PixelsPerMeter);

        bool isTopDown = CheckIfMapIsTopDown(world);

        var bodyType = BodyType.Dynamic;
        var physicsWorld = PhysicsWorldManager.GetWorld(targetPhysicsWorld);

        var aetherBody = isTopDown
            ? physicsWorld.CreateCircle(8f / PhysicsSettings.PixelsPerMeter, 1f, initialPosition, bodyType)
            : physicsWorld.CreateCapsule(14f / PhysicsSettings.PixelsPerMeter, 5f / PhysicsSettings.PixelsPerMeter, 1f, initialPosition, 0f, bodyType);

        aetherBody.FixedRotation = true;
        aetherBody.IsBullet = true;
        aetherBody.Tag = uniqueId;

        if (aetherBody.FixtureList.Count > 0)
        {
           aetherBody.FixtureList[0].CollisionCategories = PhysicsLayers.LocalPlayer;
           // ARCHITECTURE FIX: Removed RemotePlayer so co-op players do not physically block each other
           aetherBody.FixtureList[0].CollidesWith = PhysicsLayers.Environment | PhysicsLayers.EnemyAndProjectiles;
        }

        SteamId safeOwnerId = SteamManager.IsSteamActive ? SteamClient.SteamId : (SteamId)0;
        var classDef = ClassRegistry.GetClass(classId);

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
           .Set(new MovementCapabilities { MoveSpeed = classDef.MovementSpeed, JumpForce = classDef.JumpForce })
           .Set(new NetworkOwner { Value = safeOwnerId })
           .Set(new NetworkId { Value = uniqueId })
           .Set(new NetworkSequence { LatestSequence = 0, TimeSinceLastPacket = 0f })
           .Set(new FacingDirection { Value = 1 })
           .Set(new BaseCombatComponents.Health { Current = classDef.BaseHealth, Max = classDef.BaseHealth })
           .Set(new BaseCombatComponents.CombatAlignment { Value = BaseCombatComponents.Alignment.Friendly })
           .Set(new PhysicsComponents.PhysicsBody { Value = aetherBody })
           .Set(new PhysicsDimension { Name = targetPhysicsWorld });

        if (isTopDown) e.Add<TopDownTag>();
        else e.Add<SidescrollerTag>();

        NetworkRegistry.Add(uniqueId, e);
        return e;
    }

    public static Entity CreateRemote(Flecs.NET.Core.World world, string entityKey, PlayerTransformPacket packet, SteamId senderId, string targetPhysicsWorld = "MacroSpace")
    {
        var startPos = new AetherVector2(packet.X / PhysicsSettings.PixelsPerMeter, packet.Y / PhysicsSettings.PixelsPerMeter);

        bool isTopDown = CheckIfMapIsTopDown(world);
        var physicsWorld = PhysicsWorldManager.GetWorld(targetPhysicsWorld);

        var aetherBody = isTopDown
            ? physicsWorld.CreateCircle(8f / PhysicsSettings.PixelsPerMeter, 1f, startPos, BodyType.Kinematic)
            : physicsWorld.CreateCapsule(14f / PhysicsSettings.PixelsPerMeter, 5f / PhysicsSettings.PixelsPerMeter, 1f, startPos, 0f, BodyType.Kinematic);

        aetherBody.Tag = packet.EntityNetworkSequenceId;
        var classDef = ClassRegistry.GetClass(packet.CharacterClassId);

        Entity e = world.Entity(entityKey)
            .Add<RemotePlayerTag>()
            .Add<MatchEntityTag>()
            .Set(new Position { X = packet.X, Y = packet.Y })
            .Set(new PreviousPosition { X = packet.X, Y = packet.Y })
            .Set(new TargetPosition { X = packet.X, Y = packet.Y })
            .Set(new Velocity { X = packet.Vx, Y = packet.Vy })
            .Set(new CharacterClass { Id = packet.CharacterClassId })
            .Set(new MovementCapabilities { MoveSpeed = classDef.MovementSpeed, JumpForce = classDef.JumpForce })
            .Set(new NetworkOwner { Value = senderId })
            .Set(new NetworkId { Value = packet.EntityNetworkSequenceId })
            .Set(new NetworkSequence { LatestSequence = packet.SequenceNumber, TimeSinceLastPacket = 0f })
            .Set(new FacingDirection { Value = packet.FacingDirection })
            .Set(new BaseCombatComponents.Health { Current = classDef.BaseHealth, Max = classDef.BaseHealth })
            .Set(new BaseCombatComponents.CombatAlignment { Value = BaseCombatComponents.Alignment.Friendly })
            .Set(new PhysicsComponents.PhysicsBody { Value = aetherBody })
            .Set(new PhysicsDimension { Name = targetPhysicsWorld });

        if (isTopDown) e.Add<TopDownTag>();
        else e.Add<SidescrollerTag>();

        NetworkRegistry.Add(packet.EntityNetworkSequenceId, e);
        return e;
    }
}
