using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using MyGame.Engine.Platform;
using MyGame.Engine.Core;
using MyGame.Engine.Platform.Networking;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class InteractionSystem
{
    public static void Register(Flecs.NET.Core.World world)
    {
        var portalQuery = world.QueryBuilder<Position, PortalComponent, PhysicsDimension>().Build();
        var seatQuery = world.QueryBuilder<Position, PhysicsDimension, PilotSeatComponent>().Build();
        var interactiveQuery = world.QueryBuilder<Position, WorldMark, NetworkId, PhysicsDimension>().With<InteractableTag>().Build();
        var exteriorShipInteractQuery = world.QueryBuilder<Position, ShipVehicleComponent, PhysicsDimension>().With<InteractableTag>().Build();

        world.System<Position, LocalInput, PhysicsDimension>("PlayerInteractionInputSystem")
            .Kind(Ecs.PreUpdate)
            .With<LocalPlayerTag>()
            .Each((Entity player, ref Position playerPos, ref LocalInput input, ref PhysicsDimension playerDim) =>
            {
                if (!InputManager.ConsumeAction(GameActions.Interact)) return;

                var net = NetworkServiceLocator.Provider;

                if (player.Has<HelmControl>())
                {
                    var helm = player.Get<HelmControl>();

                    if (helm.ControlledVehicle.Id != 0 && helm.ControlledVehicle.IsAlive())
                    {
                        helm.ControlledVehicle.Remove<LocalInput>();

                        if (helm.ControlledVehicle.Has<NetworkId>())
                        {
                            ulong hostId = net.HostId ?? net.LocalUserId;
                            ulong targetNetId = helm.ControlledVehicle.Get<NetworkId>().Value;
                            DistributedEventSystem.BroadcastAndApplyEvent(targetNetId, (byte)GameEventType.ClaimAuthority, 0, 0f, hostId);
                        }
                    }

                    player.Remove<HelmControl>();
                    EngineLogger.Log("Player left the helm control.", "SYSTEM");
                    return;
                }

                float px = playerPos.X;
                float py = playerPos.Y;
                string currentDim = playerDim.Name;

                bool initiatedTransfer = false;

                exteriorShipInteractQuery.Each((Entity shipEnt, ref Position sPos, ref ShipVehicleComponent sComp, ref PhysicsDimension sDim) =>
                {
                    if (initiatedTransfer || sDim.Name != currentDim) return;

                    // ARCHITECTURE FIX: Cannot board a ship that is currently flying above you
                    if (shipEnt.Has<VehicleFlightState>() && shipEnt.Get<VehicleFlightState>().AltitudeRatio > 0.1f) return;

                    Vector2 rotatedOffset = Vector2.Transform(sComp.DoorLocalOffset, Matrix.CreateRotationZ(sPos.Rotation));
                    float doorX = sPos.X + rotatedOffset.X;
                    float doorY = sPos.Y + rotatedOffset.Y;

                    if (Vector2.DistanceSquared(new Vector2(px, py), new Vector2(doorX, doorY)) < 784f)
                    {
                        player.Set(new DimensionTransferRequest
                        {
                            TargetDimension = sComp.InteriorDimensionName,
                            SnapToInteriorAirlock = true
                        });
                        initiatedTransfer = true;
                    }
                });

                if (initiatedTransfer) return;

                portalQuery.Each((Entity portalEnt, ref Position pPos, ref PortalComponent pComp, ref PhysicsDimension pDim) =>
                {
                    if (initiatedTransfer || pDim.Name != currentDim) return;

                    if (Vector2.DistanceSquared(new Vector2(px, py), new Vector2(pPos.X, pPos.Y)) < 784f)
                    {
                        if (pComp.IsVehicleExit && pComp.ParentVehicleNetId != 0)
                        {
                            string parentDimension = "MacroSpace";
                            Entity vehicle = NetworkRegistry.GetEntity(pComp.ParentVehicleNetId);

                            if (vehicle.Id != 0 && vehicle.IsAlive() && vehicle.Has<PhysicsDimension>())
                            {
                                // ARCHITECTURE FIX: Immersion interlock. Cannot jump out of the airlock into deep space mid-flight.
                                if (vehicle.Has<VehicleFlightState>() && vehicle.Get<VehicleFlightState>().AltitudeRatio > 0.1f)
                                {
                                    EngineLogger.Log("Cannot exit vehicle mid-flight. You must land first.", "WARNING");
                                    return;
                                }

                                parentDimension = vehicle.Get<PhysicsDimension>().Name;
                            }

                            player.Set(new DimensionTransferRequest
                            {
                                TargetDimension = parentDimension,
                                ExitFromVehicleNetId = pComp.ParentVehicleNetId
                            });
                        }
                        else
                        {
                            player.Set(new DimensionTransferRequest
                            {
                                TargetDimension = pComp.DestinationDimension,
                                LeavingDimension = currentDim
                            });
                        }
                        initiatedTransfer = true;
                    }
                });

                if (initiatedTransfer) return;

                seatQuery.Each((ref Position sPos, ref PhysicsDimension sDim, ref PilotSeatComponent seat) =>
                {
                    if (sDim.Name != currentDim) return;

                    if (Vector2.DistanceSquared(new Vector2(px, py), new Vector2(sPos.X, sPos.Y)) < 784f)
                    {
                        Entity targetedVehicle = NetworkRegistry.GetEntity(seat.VehicleNetId);

                        if (targetedVehicle.Id != 0 && targetedVehicle.IsAlive())
                        {
                            targetedVehicle.Add<TopDownTag>();
                            player.Set(new HelmControl { ControlledVehicle = targetedVehicle });

                            if (targetedVehicle.Has<NetworkId>())
                            {
                                ulong myId = net.LocalUserId;
                                ulong targetNetId = targetedVehicle.Get<NetworkId>().Value;
                                DistributedEventSystem.BroadcastAndApplyEvent(targetNetId, (byte)GameEventType.ClaimAuthority, 0, 0f, myId);
                            }
                            return;
                        }
                    }
                });

                interactiveQuery.Each((Entity genEnt, ref Position gPos, ref WorldMark mark, ref NetworkId netId, ref PhysicsDimension iDim) =>
                {
                    if (iDim.Name != currentDim) return;

                    if (Vector2.DistanceSquared(new Vector2(px, py), new Vector2(gPos.X, gPos.Y)) < 784f)
                    {
                        int newState = mark.InteractionState == 0 ? 1 : 0;
                        DistributedEventSystem.BroadcastAndApplyEvent(netId.Value, (byte)GameEventType.InteractSwitch, newState);
                    }
                });
            });
    }
}
