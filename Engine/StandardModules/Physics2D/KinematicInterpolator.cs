using System;
using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class KinematicInterpolator
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

				if (e.Has<PhysicsComponents.PhysicsBody>())
				{
					var pBody = e.Get<PhysicsComponents.PhysicsBody>();
					if (pBody.Value != null)
					{
						var body = pBody.Value;
						body.Position = new nkast.Aether.Physics2D.Common.Vector2(
							pos.X / PhysicsSettings.PixelsPerMeter, // ARCHITECTURE FIX: Replaced hardcoded 32f
							pos.Y / PhysicsSettings.PixelsPerMeter);

						body.LinearVelocity = nkast.Aether.Physics2D.Common.Vector2.Zero;
					}
				}
			});
	}
}
