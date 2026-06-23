using Flecs.NET.Core;
using nkast.Aether.Physics2D.Dynamics;
using MyGame.Engine.Core;
using MyGame.Game.Core;
using MyGame.Prefabs;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class DimensionTransferSystem
{
    public static void Register(Flecs.NET.Core.World world)
    {
        world.System<PhysicsComponents.PhysicsBody, Position, PreviousPosition, PhysicsDimension, DimensionTransferRequest>("DimensionTransferSystem")
            .Kind(Ecs.PreUpdate)
            .Each((Entity e, ref PhysicsComponents.PhysicsBody pBody, ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension currentDim, ref DimensionTransferRequest request) =>
            {
                if (pBody.Value != null && pBody.Value.World != null)
                {
                    pBody.Value.World.Remove(pBody.Value);
                }

                currentDim.Name = request.TargetDimension;

                // ARCHITECTURE FIX: Instant translation. No disk parsing.
                // Remote players rely on their exact packet coordinates. Local players rely on LevelManager snapping.
                pos.X = request.SpawnX;
                pos.Y = request.SpawnY;
                prevPos.X = request.SpawnX;
                prevPos.Y = request.SpawnY;

                var newWorld = PhysicsWorldManager.GetWorld(request.TargetDimension);
                var initialPos = new nkast.Aether.Physics2D.Common.Vector2(pos.X / PhysicsSettings.PixelsPerMeter, pos.Y / PhysicsSettings.PixelsPerMeter);

                Body newBody;
                if (e.Has<TopDownTag>())
                {
                    newBody = newWorld.CreateCircle(8f / PhysicsSettings.PixelsPerMeter, 1f, initialPos, BodyType.Dynamic);
                }
                else
                {
                    newBody = newWorld.CreateCapsule(14f / PhysicsSettings.PixelsPerMeter, 5f / PhysicsSettings.PixelsPerMeter, 1f, initialPos, 0f, BodyType.Dynamic);
                }

                newBody.FixedRotation = true;

                if (e.Has<MyGame.Engine.StandardModules.Combat.BaseCombatComponents.ProjectileTag>())
                    newBody.IsBullet = true;

                if (newBody.FixtureList.Count > 0)
                {
                    bool isRemote = e.Has<RemotePlayerTag>();
                    newBody.FixtureList[0].CollisionCategories = isRemote ? PhysicsLayers.RemotePlayer : PhysicsLayers.LocalPlayer;
                    newBody.FixtureList[0].CollidesWith = PhysicsLayers.Environment | PhysicsLayers.EnemyAndProjectiles;
                }

                e.Set(new PhysicsComponents.PhysicsBody { Value = newBody });
                e.Remove<DimensionTransferRequest>();

                EngineLogger.Log($"Entity {e.Id} transferred to dimension: {request.TargetDimension}", "SYSTEM");
            });
    }
}
