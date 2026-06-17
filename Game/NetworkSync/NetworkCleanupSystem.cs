using Flecs.NET.Core;
using MyGame.Engine.Networking;
using MyGame.Engine.Core;

using MyGame.Game.Core;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;

namespace MyGame.Game.NetworkSync;

public static class NetworkCleanupSystem
{
	public static void Register(World world)
	{
		world.System<NetworkOwner, NetworkId>("NetworkDisconnectSweepSystem")
			.With<RemotePlayerTag>()
			.Kind(Ecs.PreUpdate)
			.Interval(1.0f)
			.Each((Iter it, int row, ref NetworkOwner owner, ref NetworkId netId) =>
			{
				Entity e = it.Entity(row);

				if (!SteamManager.CurrentLobby.HasValue)
				{
					e.Destruct();
					return;
				}

				if (!SteamManager.ActiveLobbyMembers.Contains(owner.Value.Value))
				{
					EngineLogger.Log($"Player {owner.Value.Value} left the lobby. Purging native proxy.", "NETWORK");
					e.Destruct();
				}
			});
	}
}
