using nkast.Aether.Physics2D.Dynamics;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class PhysicsLayers
{
	public const Category Environment = Category.Cat1;
	public const Category LocalPlayer = Category.Cat2;
	public const Category RemotePlayer = Category.Cat3;
	public const Category EnemyAndProjectiles = Category.Cat4;
}

public static class PhysicsComponents
{
	public struct PhysicsBody
	{
		public Body Value;
	}
}

public struct LinearDriveTag { }
public struct RotationalDriveTag { }
