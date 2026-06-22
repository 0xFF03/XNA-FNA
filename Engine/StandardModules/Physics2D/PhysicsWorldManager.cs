using System.Collections.Generic;
using nkast.Aether.Physics2D.Dynamics;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class PhysicsWorldManager
{
	private static readonly Dictionary<string, World> _worlds = new();

	public static World CreateWorld(string worldId, AetherVector2 gravity)
	{
		var newWorld = new World(gravity);
		_worlds[worldId] = newWorld;
		return newWorld;
	}

	public static World GetWorld(string worldId)
	{
		if (_worlds.TryGetValue(worldId, out var world)) return world;

		Core.EngineLogger.Log($"Physics world '{worldId}' not found. Creating default.", "WARNING");
		return CreateWorld(worldId, AetherVector2.Zero);
	}

	public static void StepAll(float deltaTime)
	{
		foreach (var world in _worlds.Values)
		{
			world.Step(deltaTime);
		}
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
	}
}
