using Microsoft.Xna.Framework;

namespace MyGame.Game.Core;

public struct LocalInput
{
	public float AxisX;
	public float AxisY;
	public bool JumpJustPressed;
	public bool FlightJustPressed;
	public Vector2 WorldMousePosition;
}
