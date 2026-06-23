using System.Collections.Generic;
using Flecs.NET.Core;
using nkast.Aether.Physics2D.Dynamics;

using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;
using AetherWorld = nkast.Aether.Physics2D.Dynamics.World;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class PhysicsWorldManager
{
	private static readonly Dictionary<string, AetherWorld> _worlds = new();

	// ARCHITECTURE FIX: Only dimensions present in this set will consume CPU cycles.
	public static readonly HashSet<string> ActiveDimensions = new();

	public static void Register(Flecs.NET.Core.World ecsWorld)
	{
		ecsWorld.System("PhysicsSimulationSystem")
			.Kind(Ecs.PostUpdate)
			.Iter((Iter it) =>
			{
				foreach (var dimName in ActiveDimensions)
				{
					if (_worlds.TryGetValue(dimName, out var world))
					{
						world.Step(it.DeltaTime());
					}
				}
			});
	}

	public static AetherWorld CreateWorld(string worldId, AetherVector2 gravity)
	{
		var newWorld = new AetherWorld(gravity);
		_worlds[worldId] = newWorld;
		return newWorld;
	}

	public static AetherWorld GetWorld(string worldId)
	{
		if (_worlds.TryGetValue(worldId, out var world)) return world;

		Core.EngineLogger.Log($"Physics world '{worldId}' not found. Creating default.", "WARNING");
		return CreateWorld(worldId, AetherVector2.Zero);
	}

	public static void ClearWorld(string worldId)
	{
		if (_worlds.TryGetValue(worldId, out var world))
		{
			world.Clear();
		}
	}

	public static void ClearAll()
	{
		foreach (var world in _worlds.Values) world.Clear();
		_worlds.Clear();
		ActiveDimensions.Clear();
	}
}
