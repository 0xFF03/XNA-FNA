using System.Collections.Generic;
using Flecs.NET.Core;

namespace MyGame.Gameplay.Networking;

public class RemoteSessionManager
{
	private readonly Dictionary<string, Entity> _sessions = new();

	public bool TryGetPlayer(string entityKey, out Entity shadowEntity)
	{
		return _sessions.TryGetValue(entityKey, out shadowEntity);
	}

	public void RegisterPlayer(string entityKey, Entity shadowEntity)
	{
		_sessions.TryAdd(entityKey, shadowEntity);
	}

	public void ClearSessions()
	{
		_sessions.Clear();
	}
}
