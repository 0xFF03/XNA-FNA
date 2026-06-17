using Flecs.NET.Core;
using MyGame.Game.Physics;

namespace MyGame.Game.Core;

public static class TransformSystems
{
	public static void Register(World world)
	{
		world.System<Position, PreviousPosition>("StorePreviousPositionSystem")
			.Kind(Ecs.PreUpdate)
			.Each((ref Position pos, ref PreviousPosition prevPos) =>
			{
				prevPos.X = pos.X;
				prevPos.Y = pos.Y;
			});

		world.Observer<PhysicsBody>("PhysicsBodyCleanupObserver")
			.Event(Ecs.OnRemove)
			.Each((ref PhysicsBody pBody) =>
			{
				if (pBody.Value != null)
				{
					var body = (nkast.Aether.Physics2D.Dynamics.Body)pBody.Value;
					if (MyGame.Game1.Instance.PhysicsWorld.BodyList.Contains(body))
					{
						MyGame.Game1.Instance.PhysicsWorld.Remove(body);
					}
				}
			});
	}
}
