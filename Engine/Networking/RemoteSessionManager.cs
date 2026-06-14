using System.Collections.Generic;
using Flecs.NET.Core;

namespace MyGame.Gameplay.Networking;

public class RemoteSessionManager
{
	private readonly Dictionary<ulong, Entity> sessions = new();

	public bool TryGetPlayer(ulong networkId, out Entity shadowEntity)
	{
		return sessions.TryGetValue(networkId, out shadowEntity);
	}

	public void RegisterPlayer(ulong networkId, Entity shadowEntity)
	{
		sessions.TryAdd(networkId, shadowEntity);
	}

	public void RemoveSession(ulong networkId)
	{
		sessions.Remove(networkId);
	}

	public void ClearSessions()
	{
		sessions.Clear();
	}
}
