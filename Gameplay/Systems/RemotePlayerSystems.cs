using Flecs.NET.Core;
using MyGame.Gameplay.Components;

namespace MyGame.Gameplay.Systems;

public static class RemotePlayerSystems
{
	private const float LerpSpeedMultiplier = 15f;

	public static void Register(World world)
	{
		world.System<Position, TargetPosition, Velocity>("ApplyRemoteLerpSystem")
			.With<RemotePlayerTag>()
			.Each((Iter it, int _, ref Position pos, ref TargetPosition target, ref Velocity vel) =>
			{
				float dt = it.DeltaTime();

				// Extrapolate the remote target with its current velocity to counteract network lag spikes
				target.X += vel.X * dt;
				target.Y += vel.Y * dt;

				// Safely clamped smoothing step
				float t = System.Math.Min(LerpSpeedMultiplier * dt, 1f);
				pos.X += (target.X - pos.X) * t;
				pos.Y += (target.Y - pos.Y) * t;
			});
	}
}
