using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;

namespace MyGame.Game.Core;

public struct SidescrollerTag { }
public struct TopDownTag { }

public struct MovementCapabilities { public float MoveSpeed; public float JumpForce; }
public struct CharacterClass { public int Id; }
public struct GroundState { public bool IsGrounded; public float CoyoteTimer; }

public struct LocalPlayerTag { }
public struct RemotePlayerTag { }
public struct MatchEntityTag { }

[Flags]
public enum AltitudeLayer : ushort
{
	Submerged = 1 << 0,
	Surface = 1 << 1,
	Airborne = 1 << 2,
	Orbit = 1 << 3
}

public struct Altitude { public AltitudeLayer Current; }
public struct HelmControl { public Entity ControlledVehicle; }

public struct DimensionTransferRequest
{
	public string TargetDimension;
	public float ExplicitSpawnX;
	public float ExplicitSpawnY;
	public bool SnapToInteriorAirlock;
	public ulong ExitFromVehicleNetId;
	public string LeavingDimension;
}

public struct TopologicalTransferTag { public string LeavingDimension; }
public struct InteractableTag { }

public struct PortalComponent
{
	public string DestinationDimension;
	public bool IsVehicleExit;
	public ulong ParentVehicleNetId;
}

public struct PilotSeatComponent { public ulong VehicleNetId; }
public struct GunnerSeatComponent { public ulong VehicleNetId; }

public struct ShipVehicleComponent
{
	public string TextureName;
	public Vector2 DoorLocalOffset;
	public string InteriorDimensionName;
}

public struct ShipEngine
{
	public float CurrentThrust;
}

// ARCHITECTURE FIX: Native flight transition tracker
public struct VehicleFlightState
{
	public bool TargetFlying;
	public float AltitudeRatio; // 0.0 = Landed, 1.0 = Max Altitude
}

public struct WorldMark
{
	public string UniqueMarkId;
	public int InteractionState;
}
