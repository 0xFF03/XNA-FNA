using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MyGame.Engine.Core;
using MyGame.Engine.StandardModules.Physics2D;

using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Engine.Platform;

public static class LevelManager
{
    private static readonly Dictionary<string, LevelData> _levelCache = new();

    // Pure Engine Event: Blindly passes unmanaged data up to the Game Domain
    public static Action<Flecs.NET.Core.World, LdtkEntityInstance, string, ulong, ulong>? OnEntityParsed;

    public static void Initialize(Flecs.NET.Core.World ecsWorld) { }

    public static void UnloadAll() => _levelCache.Clear();

    public static LevelData? GetCachedLevel(string dimensionName) => _levelCache.TryGetValue(dimensionName, out var data) ? data : null;

    public static void EnsureDimensionLoaded(Flecs.NET.Core.World ecsWorld, string targetDimension)
    {
        if (_levelCache.ContainsKey(targetDimension)) return;

        string mapPath = "Maps/Level1.ldtk"; // Base default, the Game Domain manages actual profile states

        string lookupKey = targetDimension;
        if (lookupKey.Contains("_"))
        {
            int index = lookupKey.LastIndexOf('_');
            lookupKey = lookupKey.Substring(0, index);
        }

        var loadedRoom = MapLoader.LoadSingleLevel(mapPath, lookupKey);
        _levelCache[targetDimension] = loadedRoom;

        var macroGravity = loadedRoom.IsTopDown ? new AetherVector2(0f, 0f) : new AetherVector2(0f, 20f);
        var macroWorld = PhysicsWorldManager.CreateWorld(targetDimension, macroGravity);

        var levelStaticBody = macroWorld.CreateBody(AetherVector2.Zero, 0f, nkast.Aether.Physics2D.Dynamics.BodyType.Static);
        foreach (var col in loadedRoom.Collisions)
        {
            float physX = (col.X + col.Width / 2f) / PhysicsSettings.PixelsPerMeter;
            float physY = (col.Y + col.Height / 2f) / PhysicsSettings.PixelsPerMeter;
            float physW = col.Width / PhysicsSettings.PixelsPerMeter;
            float physH = col.Height / PhysicsSettings.PixelsPerMeter;

            var fixture = levelStaticBody.CreateRectangle(physW, physH, 1f, new AetherVector2(physX, physY));
            fixture.Friction = 0.3f;
            fixture.CollisionCategories = PhysicsLayers.Environment;
        }

        SpawnMapEntities(ecsWorld, loadedRoom, targetDimension);
        EngineLogger.Log($"Dynamically generated isolated physics dimension: {targetDimension}", "SYSTEM");
    }

    private static void SpawnMapEntities(Flecs.NET.Core.World ecsWorld, LevelData loadedRoom, string activeDimension)
    {
        var net = Networking.NetworkServiceLocator.Provider;
        ulong hostId = net.HostId ?? net.LocalUserId;

        foreach (var interactable in loadedRoom.Interactables)
        {
            ulong deterministicNetId = StandardModules.Multiplayer.NetworkIdGenerator.GetDeterministicNetworkId(interactable.Iid);

            Flecs.NET.Core.Entity existing = StandardModules.Multiplayer.NetworkRegistry.GetEntity(deterministicNetId);
            if (existing.Id != 0 && existing.IsAlive()) continue;

            OnEntityParsed?.Invoke(ecsWorld, interactable, activeDimension, deterministicNetId, hostId);
        }
    }
}
