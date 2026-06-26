using nkast.Aether.Physics2D.Dynamics;
using MyGame.Engine.Platform.Networking;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Game.Core;
using MyGame.Game.Registries;

using FlecsWorld = Flecs.NET.Core.World;
using FlecsEntity = Flecs.NET.Core.Entity;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Prefabs;

public static class PlayerFactory
{
    private static bool CheckIfMapIsTopDown(FlecsWorld world)
    {
        // ARCHITECTURE FIX: O(1) direct entity lookup. Zero allocations, no query leaks.
        var mapEntity = world.Entity("GlobalMapData");
        if (mapEntity.IsAlive() && mapEntity.Has<MapComponents.MapInstance>())
        {
            return mapEntity.Get<MapComponents.MapInstance>().Data.IsTopDown;
        }
        return true;
    }

    public static FlecsEntity CreateLocal(FlecsWorld world, int classId, ulong uniqueId, float spawnX, float spawnY, string targetPhysicsWorld = "MacroSpace")
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
           aetherBody.FixtureList[0].CollidesWith = PhysicsLayers.Environment | PhysicsLayers.EnemyAndProjectiles;
        }

        ulong safeOwnerId = NetworkServiceLocator.Provider.IsActive ? NetworkServiceLocator.Provider.LocalUserId : 0;
        var classDef = ClassRegistry.GetClass(classId);

        FlecsEntity e = world.Entity(entityLookupName)
           .Add<LocalPlayerTag>()
           .Add<MatchEntityTag>()
           .Add<LinearDriveTag>()
           .Set(new LocalInput { AxisX = 0, AxisY = 0, JumpJustPressed = false, WorldMousePosition = Microsoft.Xna.Framework.Vector2.Zero })
           .Set(new GroundState { IsGrounded = false, CoyoteTimer = 0f })
           .Set(new Position { X = spawnX, Y = spawnY, Rotation = 0f })
           .Set(new PreviousPosition { X = spawnX, Y = spawnY, Rotation = 0f })
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

    public static FlecsEntity CreateRemote(FlecsWorld world, string entityKey, PlayerTransformPacket packet, ulong senderId, string targetPhysicsWorld = "MacroSpace")
    {
        var startPos = new AetherVector2(packet.X / PhysicsSettings.PixelsPerMeter, packet.Y / PhysicsSettings.PixelsPerMeter);

        bool isTopDown = CheckIfMapIsTopDown(world);
        var physicsWorld = PhysicsWorldManager.GetWorld(targetPhysicsWorld);

        var aetherBody = isTopDown
            ? physicsWorld.CreateCircle(8f / PhysicsSettings.PixelsPerMeter, 1f, startPos, BodyType.Kinematic)
            : physicsWorld.CreateCapsule(14f / PhysicsSettings.PixelsPerMeter, 5f / PhysicsSettings.PixelsPerMeter, 1f, startPos, 0f, BodyType.Kinematic);

        aetherBody.Tag = packet.EntityNetworkSequenceId;
        var classDef = ClassRegistry.GetClass(packet.CharacterClassId);

        FlecsEntity e = world.Entity(entityKey)
            .Add<RemotePlayerTag>()
            .Add<MatchEntityTag>()
            .Add<LinearDriveTag>()
            .Set(new Position { X = packet.X, Y = packet.Y, Rotation = packet.Rotation })
            .Set(new PreviousPosition { X = packet.X, Y = packet.Y, Rotation = packet.Rotation })
            .Set(new TargetPosition { X = packet.X, Y = packet.Y, Rotation = packet.Rotation })
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
