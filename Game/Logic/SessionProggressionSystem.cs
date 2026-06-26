using Flecs.NET.Core;
using MyGame.Engine.Platform.Networking;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Game.Core;

namespace MyGame.Game.Logic;

public static class SessionProgressionSystem
{
	private static float _accumulatedPlaytime = 0f;

	public static void Register(World world)
	{
		world.System<Position, PhysicsDimension, BaseCombatComponents.Health>("AutoSaveProgressionSystem")
			.With<LocalPlayerTag>()
			.Interval(1.0f)
			.Each((ref Position pos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
			{
				if (SaveManager.CurrentProfile == null) return;

				_accumulatedPlaytime += 1.0f;

				// --- DISK I/O TEMPORARILY DISABLED PER ARCHITECT INSTRUCTIONS ---
				// The auto-save 300-second tick is disabled to prevent disk access during Network testing.
			});
	}
}
