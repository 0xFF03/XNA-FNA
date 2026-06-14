using System;
using Flecs.NET.Core;
using MyGame.Engine.Networking;
using MyGame.Gameplay.Components;
using MyGame.Gameplay.Networking;

namespace MyGame.Gameplay.Systems;

public static class NetworkCleanupSystem
{
	public static void Register(World world, RemoteSessionManager sessionManager)
	{
		world.System<NetworkOwner, NetworkId>("NetworkDisconnectSweepSystem")
			.With<RemotePlayerTag>()
			.Kind(Ecs.PreUpdate)
			.Interval(1.0f) // Added optimization: Sweeps once per second instead of every frame
			.Each((Iter it, int row, ref NetworkOwner owner, ref NetworkId netId) =>
			{
				if (!SteamManager.CurrentLobby.HasValue)
				{
					ClearEntity(it, row, netId, sessionManager);
					return;
				}

				bool isStillInLobby = false;
				foreach (var member in SteamManager.CurrentLobby.Value.Members)
				{
					if (member.Id == owner.Value)
					{
						isStillInLobby = true;
						break;
					}
				}

				if (!isStillInLobby)
				{
					Console.WriteLine($"[Network Sync]: Player {owner.Value} left the lobby. Despawning proxy.");
					ClearEntity(it, row, netId, sessionManager);
				}
			});
	}

	private static void ClearEntity(Iter it, int row, NetworkId netId, RemoteSessionManager sessionManager)
	{
		Entity e = it.Entity(row);

		// Flawlessly removes the session from the dictionary to prevent memory leaks
		sessionManager.RemoveSession(netId.Value);

		// Safely deferred by the Flecs pipeline
		e.Destruct();
	}
}
