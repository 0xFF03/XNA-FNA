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

	public struct ProjectileSpawnRequest
	{
		public float StartX;
		public float StartY;
		public float VelocityX;
		public float VelocityY;
		public string TargetPhysicsWorld;
	}
}
