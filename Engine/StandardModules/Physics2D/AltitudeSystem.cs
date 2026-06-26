using Flecs.NET.Core;
using nkast.Aether.Physics2D.Dynamics;
using MyGame.Game.Core;

using FlecsWorld = Flecs.NET.Core.World;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class AltitudeSystem
{
	// ARCHITECTURE FIX: Absolute namespace qualification prevents compiler panic
	public static void Register(FlecsWorld world)
	{
		world.Observer<PhysicsComponents.PhysicsBody, Altitude>("AltitudeChangeObserver")
			.Event(Ecs.OnSet)
			.Each((Entity e, ref PhysicsComponents.PhysicsBody pBody, ref Altitude alt) =>
			{
				if (pBody.Value == null || pBody.Value.FixtureList.Count == 0) return;

				var fixture = pBody.Value.FixtureList[0];

				Category newCategory = Category.None;

				if (alt.Current.HasFlag(AltitudeLayer.Submerged)) newCategory |= Category.Cat5;
				if (alt.Current.HasFlag(AltitudeLayer.Surface))   newCategory |= Category.Cat1;
				if (alt.Current.HasFlag(AltitudeLayer.Airborne))  newCategory |= Category.Cat6;
				if (alt.Current.HasFlag(AltitudeLayer.Orbit))     newCategory |= Category.Cat7;

				fixture.CollidesWith = newCategory | PhysicsLayers.EnemyAndProjectiles;
			});
	}
}
