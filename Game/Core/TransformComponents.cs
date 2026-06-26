namespace MyGame.Game.Core;

public struct Position
{
	public float X;
	public float Y;
	public float Rotation;
}

public struct PreviousPosition
{
	public float X;
	public float Y;
	public float Rotation;
}

public struct Velocity
{
	public float X;
	public float Y;
}

public struct TargetPosition
{
	public float X;
	public float Y;
	public float Rotation;
}

public struct PreviousVelocity
{
	public float X;
	public float Y;
}

public struct FacingDirection
{
	public int Value;
}

public struct PhysicsDimension
{
	public string Name;
}
