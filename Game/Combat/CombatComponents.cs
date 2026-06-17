namespace MyGame.Game.Combat;

public struct ProjectileTag { }
public struct DeadTag { }

public struct Lifetime { public float Remaining; }
public struct Damage { public int Amount; }

// Metroidvania Core
public struct Health
{
	public int Current;
	public int Max;
}

public struct ProjectileSpawnRequest
{
	public float StartX;
	public float StartY;
	public float VelocityX;
	public float VelocityY;
}

// ARCHITECTURE FIX: The trigger for our generic sync system
public struct OutboundDistributedEvent
{
	public ulong TargetNetworkId;
	public byte EventType;
	public int IntPayload;
	public float FloatPayload;
}
