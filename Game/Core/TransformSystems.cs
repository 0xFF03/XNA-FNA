using Flecs.NET.Core;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.Core;

namespace MyGame.Game.Core;

public static class TransformSystems
{
	// ARCHITECTURE FIX: Strictly defined as Flecs.NET.Core.World
	public static void Register(Flecs.NET.Core.World world)
	{
		world.System<Position, PreviousPosition>("StorePreviousPositionSystem")
			.Kind(Ecs.PreUpdate)
			.Each((ref Position pos, ref PreviousPosition prevPos) =>
			{
				prevPos.X = pos.X;
				prevPos.Y = pos.Y;
				prevPos.Rotation = pos.Rotation;
			});

		world.Observer<PhysicsComponents.PhysicsBody>("PhysicsBodyCleanupObserver")
			.Event(Ecs.OnRemove)
			.Each((ref PhysicsComponents.PhysicsBody pBody) =>
			{
				if (pBody.Value != null)
				{
					var body = pBody.Value;
					if (body.World != null)
					{
						body.World.Remove((nkast.Aether.Physics2D.Dynamics.Body)body);
					}
				}
			});
	}
}
