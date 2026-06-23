using Flecs.NET.Core;
using Steamworks;
using MyGame.Engine.Platform;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class NetworkCleanupSystem
{
	public static void Register(World world)
	{
		world.System<NetworkOwner, NetworkId>("NetworkDisconnectSweepSystem")
			.Kind(Ecs.PreUpdate)
			.Interval(1.0f)
			.Each((Iter it, int row, ref NetworkOwner owner, ref NetworkId netId) =>
			{
				// ARCHITECTURE FIX: Abort sweep if we are playing Solo/Offline
				if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

				// If it's owned by the local client (or we are host), keep it alive
				if (owner.Value == SteamClient.SteamId) return;

				Entity e = it.Entity(row);

				if (!SteamManager.ActiveLobbyMembers.Contains(owner.Value.Value))
				{
					e.Destruct();
				}
			});
	}
}
