using Flecs.NET.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Game.Core;

namespace MyGame.Game.Logic;

public static class SessionProgressionSystem
{
	private static float _accumulatedPlaytime = 0f;
	private static float _autoSaveTimer = 0f;

	public static void Register(World world)
	{
		// Runs at a steady 1-second interval to avoid frame-delta micro-allocations
		world.System<Position, PhysicsDimension, BaseCombatComponents.Health>("AutoSaveProgressionSystem")
			.With<LocalPlayerTag>()
			.Interval(1.0f)
			.Each((ref Position pos, ref PhysicsDimension dim, ref BaseCombatComponents.Health hp) =>
			{
				if (SaveManager.CurrentProfile == null) return;

				_accumulatedPlaytime += 1.0f;

				bool isHost = !SteamManager.KnownHostId.HasValue || SteamManager.KnownHostId.Value == Steamworks.SteamClient.SteamId;

				if (isHost)
				{
					_autoSaveTimer += 1.0f;

					// Auto save every 5 minutes (300 seconds)
					if (_autoSaveTimer >= 300f)
					{
						_autoSaveTimer = 0f;
						var profile = SaveManager.CurrentProfile;
						SaveManager.PerformAutoSave(profile.CurrentMapPath, pos.X, pos.Y, hp.Current, dim.Name, _accumulatedPlaytime);
						_accumulatedPlaytime = 0f; // Reset buffer after saving
					}
				}
			});
	}
}
