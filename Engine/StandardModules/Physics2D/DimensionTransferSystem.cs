using System.Linq;
using System.Text.Json;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using MyGame.Engine.Platform;
using MyGame.Engine.Core;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class DimensionTransferSystem
{
    public static void Register(World world)
    {
        world.System<DimensionTransferRequest, Position, PreviousPosition, Velocity, PreviousVelocity, PhysicsDimension>("DimensionTransferSystem")
            .Kind(Ecs.PreUpdate)
            .Each((Iter it, int i, ref DimensionTransferRequest req, ref Position pos, ref PreviousPosition prevPos, ref Velocity vel, ref PreviousVelocity prevVel, ref PhysicsDimension dim) =>
            {
                Entity e = it.Entity(i);

                LevelManager.EnsureDimensionLoaded(it.World(), req.TargetDimension);

                float finalSpawnX = req.ExplicitSpawnX;
                float finalSpawnY = req.ExplicitSpawnY;

                // 1. Vehicle Exit Ejection Math
                if (req.ExitFromVehicleNetId != 0)
                {
                    Entity vehicle = NetworkRegistry.GetEntity(req.ExitFromVehicleNetId);
                    if (vehicle.Id != 0 && vehicle.IsAlive() && vehicle.Has<Position>() && vehicle.Has<ShipVehicleComponent>())
                    {
                        var vPos = vehicle.Get<Position>();
                        var sComp = vehicle.Get<ShipVehicleComponent>();

                        Vector2 rotatedOffset = Vector2.Transform(sComp.DoorLocalOffset, Matrix.CreateRotationZ(vPos.Rotation));
                        Vector2 ejectionDir = rotatedOffset != Vector2.Zero ? Vector2.Normalize(rotatedOffset) : new Vector2(0, 1);

                        // Push player safely past the bounds of the hull
                        Vector2 ejectionVector = ejectionDir * 32f;

                        finalSpawnX = vPos.X + rotatedOffset.X + ejectionVector.X;
                        finalSpawnY = vPos.Y + rotatedOffset.Y + ejectionVector.Y;
                    }
                }
                // 2. Interior Ship Entry Solver (Data-Driven LDtk Spawn Point)
                else if (req.SnapToInteriorAirlock)
                {
                    var targetLevel = LevelManager.GetCachedLevel(req.TargetDimension);
                    if (targetLevel != null)
                    {
                        var door = targetLevel.Interactables.FirstOrDefault(inter => inter.Identifier == "AirlockDoor");
                        if (door != null)
                        {
                            // Default Fallback
                            float doorW = door.Width > 0 ? door.Width : 16f;
                            float doorH = door.Height > 0 ? door.Height : 16f;
                            finalSpawnX = door.Px[0] + (doorW / 2f);
                            finalSpawnY = door.Px[1] + (doorH / 2f);

                            // ARCHITECTURE FIX: Exact point mapping from LDtk ArrivalPoint
                            var arrivalField = door.FieldInstances?.FirstOrDefault(f => f.Identifier.Equals("ArrivalPoint", System.StringComparison.OrdinalIgnoreCase));
                            if (arrivalField != null && arrivalField.Value is JsonElement je && je.ValueKind == JsonValueKind.Object)
                            {
                                if (je.TryGetProperty("cx", out var cxProp) && je.TryGetProperty("cy", out var cyProp))
                                {
                                    finalSpawnX = (cxProp.GetSingle() * 16f) + 8f;
                                    finalSpawnY = (cyProp.GetSingle() * 16f) + 8f;
                                }
                            }
                        }
                    }
                }
                // 3. Static Level Portals
                else if (req.ExplicitSpawnX == 0 && req.ExplicitSpawnY == 0 && !string.IsNullOrEmpty(req.LeavingDimension))
                {
                    var targetLevel = LevelManager.GetCachedLevel(req.TargetDimension);
                    if (targetLevel != null)
                    {
                        foreach (var inter in targetLevel.Interactables)
                        {
                            var targetDimField = inter.FieldInstances?.FirstOrDefault(f => f.Identifier.Equals("TargetDimension", System.StringComparison.OrdinalIgnoreCase));
                            if (targetDimField != null && targetDimField.Value != null && targetDimField.Value.ToString() == req.LeavingDimension)
                            {
                                float doorW = inter.Width > 0 ? inter.Width : 16f;
                                float doorH = inter.Height > 0 ? inter.Height : 16f;
                                finalSpawnX = inter.Px[0] + (doorW / 2f);
                                finalSpawnY = inter.Px[1] + (doorH / 2f);

                                var arrivalField = inter.FieldInstances?.FirstOrDefault(f => f.Identifier.Equals("ArrivalPoint", System.StringComparison.OrdinalIgnoreCase));
                                if (arrivalField != null && arrivalField.Value is JsonElement je && je.ValueKind == JsonValueKind.Object)
                                {
                                    if (je.TryGetProperty("cx", out var cxProp) && je.TryGetProperty("cy", out var cyProp))
                                    {
                                        finalSpawnX = (cxProp.GetSingle() * 16f) + 8f;
                                        finalSpawnY = (cyProp.GetSingle() * 16f) + 8f;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }

                if (e.Has<PhysicsComponents.PhysicsBody>())
                {
                    var pBody = e.Get<PhysicsComponents.PhysicsBody>();
                    if (pBody.Value != null)
                    {
                        if (pBody.Value.World != null)
                        {
                            pBody.Value.World.Remove(pBody.Value);
                        }

                        var newWorld = PhysicsWorldManager.GetWorld(req.TargetDimension);
                        newWorld.Add(pBody.Value);

                        pBody.Value.Position = new nkast.Aether.Physics2D.Common.Vector2(
                            finalSpawnX / PhysicsSettings.PixelsPerMeter,
                            finalSpawnY / PhysicsSettings.PixelsPerMeter);

                        pBody.Value.LinearVelocity = nkast.Aether.Physics2D.Common.Vector2.Zero;
                        pBody.Value.Awake = true;
                    }
                }

                dim.Name = req.TargetDimension;

                pos.X = finalSpawnX;
                pos.Y = finalSpawnY;
                prevPos.X = finalSpawnX;
                prevPos.Y = finalSpawnY;

                if (e.Has<TargetPosition>())
                {
                    ref var tPos = ref e.GetMut<TargetPosition>();
                    tPos.X = finalSpawnX;
                    tPos.Y = finalSpawnY;
                }

                vel.X = 0f;
                vel.Y = 0f;
                prevVel.X = 0f;
                prevVel.Y = 0f;

                e.Remove<DimensionTransferRequest>();
            });
    }
}
