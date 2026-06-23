using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using MyGame.Engine.Platform;
using MyGame.Engine.Core;
using Steamworks;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class InteractionSystem
{
    private static Query<ShipVehicleComponent, PortalComponent, PhysicsDimension> _vehicleQuery;

    public static void Register(Flecs.NET.Core.World world)
    {
        var portalQuery = world.QueryBuilder<Position, PortalComponent>().Build();
        var seatQuery = world.QueryBuilder<Position>().With<PilotSeatComponent>().Build();
        var interactiveQuery = world.QueryBuilder<Position, WorldMark, NetworkId>().With<InteractableTag>().Build();

        _vehicleQuery = world.QueryBuilder<ShipVehicleComponent, PortalComponent, PhysicsDimension>().Build();

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

                        // ARCHITECTURE FIX: Relinquish authority when stepping out of the pilot seat
                        ulong hostId = SteamManager.GetLocalOrHostId();
                        ulong targetNetId = helm.ControlledVehicle.Get<NetworkId>().Value;
                        DistributedEventSystem.BroadcastAndApplyEvent(targetNetId, (byte)GameEventType.ClaimAuthority, 0, 0f, hostId);
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
                        SpawnX = px,
                        SpawnY = py
                    });
                    InputManager.ResetState();
                    return;
                }

                bool foundSeat = false;
                float closestSeatDist = 28f;
                Entity targetedVehicle = new Entity();

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
                    string currentDimension = player.Get<PhysicsDimension>().Name;

                    _vehicleQuery.Each((Entity vEnt, ref ShipVehicleComponent vComp, ref PortalComponent vPortal, ref PhysicsDimension vDim) =>
                    {
                        if (vPortal.DestinationDimension == currentDimension)
                        {
                            targetedVehicle = vEnt;
                        }
                    });

                    if (targetedVehicle.Id != 0 && targetedVehicle.IsAlive())
                    {
                        targetedVehicle.Add<TopDownTag>();
                        player.Set(new HelmControl { ControlledVehicle = targetedVehicle });

                        // ARCHITECTURE FIX: Broadcast to the lobby that YOU own the movement of this specific ship now
                        ulong myId = SteamManager.GetLocalOrHostId();
                        ulong targetNetId = targetedVehicle.Get<NetworkId>().Value;
                        DistributedEventSystem.BroadcastAndApplyEvent(targetNetId, (byte)GameEventType.ClaimAuthority, 0, 0f, myId);

                        EngineLogger.Log("Player entered the pilot seat and claimed helm authority.", "SYSTEM");
                        InputManager.ResetState();
                        return;
                    }
                }

                bool foundGeneric = false;
                float closestGenericDist = 28f;

                interactiveQuery.Each((Entity genEnt, ref Position gPos, ref WorldMark mark, ref NetworkId netId) =>
                {
                    float dist = Vector2.Distance(new Vector2(px, py), new Vector2(gPos.X, gPos.Y));
                    if (dist < closestGenericDist && !foundGeneric)
                    {
                        int newState = mark.InteractionState == 0 ? 1 : 0;
                        DistributedEventSystem.BroadcastAndApplyEvent(netId.Value, (byte)GameEventType.InteractSwitch, newState);
                        foundGeneric = true;
                    }
                });

                if (foundGeneric)
                {
                    InputManager.ResetState();
                }
            });
    }
}
