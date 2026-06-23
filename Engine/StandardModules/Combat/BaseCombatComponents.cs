namespace MyGame.Engine.StandardModules.Combat;

public static class BaseCombatComponents
{
	public struct ProjectileTag { }
	public struct DeadTag { }

	public struct Lifetime { public float Remaining; }
	public struct Damage { public int Amount; }

	public struct Health
	{
		public int Current;
		public int Max;
	}

	// ARCHITECTURE FIX: Native support for zero-friendly-fire and future healing items
	public enum Alignment : byte
	{
		Friendly,
		Hostile,
		Neutral
	}

	public struct CombatAlignment
	{
		public Alignment Value;
	}

	public struct ProjectileSpawnRequest
	{
		public float StartX;
		public float StartY;
		public float VelocityX;
		public float VelocityY;
		public string TargetPhysicsWorld;
	}
}
