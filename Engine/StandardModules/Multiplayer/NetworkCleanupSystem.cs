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
				if (!SteamManager.IsSteamActive) return;
				if (owner.Value == SteamClient.SteamId) return;

				Entity e = it.Entity(row);

				if (!SteamManager.CurrentLobby.HasValue)
				{
					e.Destruct();
					return;
				}

				if (!SteamManager.ActiveLobbyMembers.Contains(owner.Value.Value))
				{
					e.Destruct();
				}
			});
	}
}
