using System.Linq;
using Flecs.NET.Core;
using nkast.Aether.Physics2D.Dynamics;
using MyGame.Engine.Core;
using MyGame.Game.Core;
using MyGame.Game.Logic;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class DimensionTransferSystem
{
    // ARCHITECTURE FIX: Fully qualified name prevents Aether/Flecs 'World' ambiguity
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

                string mapPath = "Maps/Level1.ldtk";
                if (SaveManager.CurrentProfile != null)
                {
                    mapPath = SaveManager.CurrentProfile.CurrentMapPath;
                }

                var targetRoomData = Engine.Platform.MapLoader.LoadSingleLevel(mapPath, request.TargetDimension);

                float spawnX = targetRoomData.SpawnPoint.X;
                float spawnY = targetRoomData.SpawnPoint.Y;

                bool foundVehicle = false;
                Entity spaceship = world.Lookup("ActiveSpaceshipExterior");

                if (spaceship.Id != 0 && spaceship.IsAlive() && spaceship.Has<PhysicsDimension>())
                {
                    var vDim = spaceship.Get<PhysicsDimension>();
                    if (vDim.Name == request.TargetDimension)
                    {
                        var vPos = spaceship.Get<Position>();
                        var vComp = spaceship.Get<ShipVehicleComponent>();

                        spawnX = vPos.X + vComp.DoorLocalOffset.X;
                        spawnY = vPos.Y + vComp.DoorLocalOffset.Y + 16f;

                        foundVehicle = true;
                    }
                }

                if (!foundVehicle)
                {
                    var interiorDoor = targetRoomData.Interactables.FirstOrDefault(ent => ent.Identifier == "AirlockDoor");
                    if (interiorDoor != null)
                    {
                        spawnX = interiorDoor.Px[0];
                        spawnY = interiorDoor.Px[1] + 16f;
                    }
                }

                pos.X = spawnX;
                pos.Y = spawnY;
                prevPos.X = spawnX;
                prevPos.Y = spawnY;

                var newWorld = PhysicsWorldManager.GetWorld(request.TargetDimension);
                var initialPos = new nkast.Aether.Physics2D.Common.Vector2(spawnX / PhysicsSettings.PixelsPerMeter, spawnY / PhysicsSettings.PixelsPerMeter);

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
