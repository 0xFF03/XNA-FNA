using System.Collections.Generic;
using Flecs.NET.Core;

namespace MyGame.Engine.StandardModules.Multiplayer;

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

	// ARCHITECTURE FIX: Eliminated Nullable<Entity> to satisfy C# unsafe context rules
	public static Entity GetEntity(ulong netId)
	{
		if (_registry.TryGetValue(netId, out Entity e) && e.IsAlive())
			return e;

		return new Entity(); // Safely returns an empty entity (Id = 0)
	}

	public static void ClearAll() => _registry.Clear();
}
