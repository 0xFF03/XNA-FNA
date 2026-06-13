using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using MyGame.Gameplay.Components;

namespace MyGame.Gameplay.Systems;

public static class MovementSystems
{
	public static void Register(World world)
	{
		// 1. INPUT SYSTEM: Reads local key arrays to handle velocity direction changes
		world.System<Velocity>("InputSystem")
			.With<LocalPlayerTag>()
			.Each((ref Velocity vel) =>
			{
				var kstate = Keyboard.GetState();
				const float speed = 200f;

				vel.X = 0;
				vel.Y = 0;

				if (kstate.IsKeyDown(Keys.W) || kstate.IsKeyDown(Keys.Up)) vel.Y = -speed;
				if (kstate.IsKeyDown(Keys.S) || kstate.IsKeyDown(Keys.Down)) vel.Y = speed;
				if (kstate.IsKeyDown(Keys.A) || kstate.IsKeyDown(Keys.Left)) vel.X = -speed;
				if (kstate.IsKeyDown(Keys.D) || kstate.IsKeyDown(Keys.Right)) vel.X = speed;
			});

		// 2. MOVEMENT SYSTEM: Frame-rate independent delta physics applied to all positioning data arrays
		world.System<Position, Velocity>("ApplyMovementSystem")
			.Each((Iter it, int _, ref Position pos, ref Velocity vel) =>
			{
				float trueDelta = it.DeltaTime();
				pos.X += vel.X * trueDelta;
				pos.Y += vel.Y * trueDelta;
			});
	}
}
