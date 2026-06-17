using System;
using Microsoft.Xna.Framework;
using Flecs.NET.Core;

using MyGame.Game.Core;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;

namespace MyGame.Game.Physics;

public static class RemotePlayerSystems
{
	public static void Register(World world)
	{
		world.System<Position, TargetPosition, Velocity, NetworkSequence>("RemoteInterpolationSystem")
			.With<RemotePlayerTag>()
			.Without<LocalPlayerTag>()
			.Each((Entity e, ref Position pos, ref TargetPosition target, ref Velocity velocity, ref NetworkSequence seq) =>
			{
				float distanceX = target.X - pos.X;
				float distanceY = target.Y - pos.Y;

				if (MathF.Abs(distanceX) > 64f || MathF.Abs(distanceY) > 64f)
				{
					pos.X = target.X;
					pos.Y = target.Y;
				}
				else
				{
					pos.X = MathHelper.Lerp(pos.X, target.X, 0.25f);
					pos.Y = MathHelper.Lerp(pos.Y, target.Y, 0.25f);
				}

				if (e.Has<PhysicsBody>())
				{
					var pBody = e.Get<PhysicsBody>();
					if (pBody.Value != null)
					{
						var body = (nkast.Aether.Physics2D.Dynamics.Body)pBody.Value;
						body.Position = new nkast.Aether.Physics2D.Common.Vector2(
							pos.X / MyGame.Prefabs.PlayerFactory.PixelsPerMeter,
							pos.Y / MyGame.Prefabs.PlayerFactory.PixelsPerMeter);

						body.LinearVelocity = new nkast.Aether.Physics2D.Common.Vector2(velocity.X, velocity.Y);
					}
				}
			});
	}
}
