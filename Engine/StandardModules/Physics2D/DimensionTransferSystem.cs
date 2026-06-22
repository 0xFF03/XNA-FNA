using Flecs.NET.Core;
using nkast.Aether.Physics2D.Dynamics;
using MyGame.Engine.Core;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class DimensionTransferSystem
{
    // ARCHITECTURE FIX: Explicitly mapped to Flecs World
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
                pos.X = request.SpawnX;
                pos.Y = request.SpawnY;
                prevPos.X = request.SpawnX;
                prevPos.Y = request.SpawnY;

                var newWorld = PhysicsWorldManager.GetWorld(request.TargetDimension);

                var initialPos = new nkast.Aether.Physics2D.Common.Vector2(request.SpawnX / PhysicsSettings.PixelsPerMeter, request.SpawnY / PhysicsSettings.PixelsPerMeter);

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
                {
                    newBody.IsBullet = true;
                }

                if (newBody.FixtureList.Count > 0)
                {
                    newBody.FixtureList[0].CollisionCategories = MyGame.Prefabs.PhysicsLayers.LocalPlayer;
                    newBody.FixtureList[0].CollidesWith = MyGame.Prefabs.PhysicsLayers.Environment | MyGame.Prefabs.PhysicsLayers.EnemyAndProjectiles | MyGame.Prefabs.PhysicsLayers.RemotePlayer;
                }

                e.Set(new PhysicsComponents.PhysicsBody { Value = newBody });
                e.Remove<DimensionTransferRequest>();

                EngineLogger.Log($"Entity {e.Id} transferred to dimension: {request.TargetDimension}", "SYSTEM");
            });
    }
}
