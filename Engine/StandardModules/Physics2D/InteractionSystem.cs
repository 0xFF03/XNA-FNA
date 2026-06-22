using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using MyGame.Engine.Platform;
using MyGame.Engine.Core;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class InteractionSystem
{
    // ARCHITECTURE FIX: Fully qualified name prevents Aether/Flecs 'World' ambiguity
    public static void Register(Flecs.NET.Core.World world)
    {
        var portalQuery = world.QueryBuilder<Position, PortalComponent>().Build();
        var seatQuery = world.QueryBuilder<Position>().With<PilotSeatComponent>().Build();

        world.System<Position, LocalInput>("PlayerInteractionInputSystem")
            .Kind(Ecs.PreUpdate)
            .With<LocalPlayerTag>()
            .Each((Entity player, ref Position playerPos, ref LocalInput input) =>
            {
                if (!InputManager.IsActionActive(GameActions.Interact)) return;

                if (player.Has<HelmControl>())
                {
                    var helm = player.Get<HelmControl>();

                    if (helm.ControlledVehicle.Id != 0 && helm.ControlledVehicle.IsAlive())
                    {
                        helm.ControlledVehicle.Remove<LocalInput>();
                    }

                    player.Remove<HelmControl>();
                    EngineLogger.Log("Player left the helm control.", "SYSTEM");
                    InputManager.ResetState();
                    return;
                }

                float px = playerPos.X;
                float py = playerPos.Y;

                bool foundPortal = false;
                string destDimension = string.Empty;
                float closestPortalDist = 28f;

                portalQuery.Each((Entity portalEnt, ref Position pPos, ref PortalComponent pComp) =>
                {
                    float targetX = pPos.X;
                    float targetY = pPos.Y;

                    if (portalEnt.Has<ShipVehicleComponent>())
                    {
                        var shipComp = portalEnt.Get<ShipVehicleComponent>();
                        targetX += shipComp.DoorLocalOffset.X;
                        targetY += shipComp.DoorLocalOffset.Y;
                    }

                    float dist = Vector2.Distance(new Vector2(px, py), new Vector2(targetX, targetY));
                    if (dist < closestPortalDist)
                    {
                        destDimension = pComp.DestinationDimension;
                        closestPortalDist = dist;
                        foundPortal = true;
                    }
                });

                if (foundPortal)
                {
                    player.Set(new DimensionTransferRequest
                    {
                        TargetDimension = destDimension,
                        SpawnX = 120f,
                        SpawnY = 120f
                    });
                    InputManager.ResetState();
                    return;
                }

                bool foundSeat = false;
                float closestSeatDist = 28f;

                seatQuery.Each((ref Position sPos) =>
                {
                    float dist = Vector2.Distance(new Vector2(px, py), new Vector2(sPos.X, sPos.Y));
                    if (dist < closestSeatDist)
                    {
                        closestSeatDist = dist;
                        foundSeat = true;
                    }
                });

                if (foundSeat)
                {
                    Entity vehicle = world.Lookup("ActiveSpaceshipExterior");

                    if (vehicle.Id == 0 || !vehicle.IsAlive())
                    {
                        vehicle = world.Entity("ActiveSpaceshipExterior")
                            .Add<TopDownTag>()
                            .Set(new Position { X = px, Y = py })
                            .Set(new Velocity { X = 0, Y = 0 })
                            .Set(new MovementCapabilities { MoveSpeed = 12f, JumpForce = 0 });
                    }

                    player.Set(new HelmControl { ControlledVehicle = vehicle });
                    EngineLogger.Log("Player entered the pilot seat and took helm control.", "SYSTEM");
                    InputManager.ResetState();
                }
            });
    }
}
