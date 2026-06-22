using System;
using Flecs.NET.Core;

namespace MyGame.Game.Core;

// Genre Context Filtering Tags
public struct SidescrollerTag { }
public struct TopDownTag { }

public struct MovementCapabilities { public float MoveSpeed; public float JumpForce; }

public struct CharacterClass { public int Id; }

public struct GroundState { public bool IsGrounded; public float CoyoteTimer; }

public struct LocalPlayerTag { }
public struct RemotePlayerTag { }
public struct MatchEntityTag { }

// --- NEW VEHICLE & SPACE COMPONENTS ---

[Flags]
public enum AltitudeLayer : ushort
{
	Submerged = 1 << 0,
	Surface = 1 << 1,
	Airborne = 1 << 2,
	Orbit = 1 << 3
}

public struct Altitude
{
	public AltitudeLayer Current;
}

public struct HelmControl
{
	public Entity ControlledVehicle;
}

public struct DimensionTransferRequest
{
	public string TargetDimension;
	public float SpawnX;
	public float SpawnY;
}

// --- NEW REUSABLE INTERACTION COMPONENTS ---

public struct InteractableTag { }

public struct PortalComponent
{
	public string DestinationDimension;
}

public struct PilotSeatComponent { }
