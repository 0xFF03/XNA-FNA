using System.Collections.Generic;
using Flecs.NET.Core;
using MyGame.Game.Core;
using MyGame.Game.NetworkSync;

namespace MyGame.Game.NetworkSync;

public static class NetworkRegistry
{
	private static readonly Dictionary<ulong, Entity> _registry = new();

	public static void Register(World world)
	{
		world.Observer<NetworkId>("NetworkRegistryCleanup")
			.Event(Ecs.OnRemove)
			.Each((Entity e, ref NetworkId netId) =>
			{
				if (_registry.ContainsKey(netId.Value))
				{
					_registry.Remove(netId.Value);
				}
			});
	}

	public static void Add(ulong netId, Entity entity)
	{
		_registry[netId] = entity;
	}

	public static Entity? GetEntity(ulong netId)
	{
		if (_registry.TryGetValue(netId, out Entity e) && e.IsAlive())
			return e;

		return null;
	}

	public static void ClearAll() => _registry.Clear();
}
