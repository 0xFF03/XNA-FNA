using System;
using System.Collections.Generic;
using Flecs.NET.Core;
using MyGame.Engine.Core;
using MyGame.Game.Core;

namespace MyGame.Game.Logic;

public static class SaveManager
{
    public static SaveProfile? CurrentProfile { get; private set; }
    public static bool HasSaves { get; private set; } = false;

    private static Query<Position, WorldMark> _marksQuery;
    private static readonly Dictionary<int, SaveProfile> _ramProfiles = new();

    // ARCHITECTURE FIX: Strictly defined as Flecs.NET.Core.World
    public static void Initialize(Flecs.NET.Core.World ecsWorld)
    {
        _marksQuery = ecsWorld.QueryBuilder<Position, WorldMark>().Build();
    }

    public static SaveProfile? GetLatestProfile()
    {
        return CurrentProfile;
    }

    public static SaveProfile?[] GetDisplayProfiles()
    {
        var result = new SaveProfile?[10];
        foreach (var p in _ramProfiles.Values)
        {
            if (p.Id >= 1 && p.Id <= 10) result[p.Id - 1] = p;
        }
        return result;
    }

    public static void CreateNewProfile(int slotId, string name, int classId, string startingMap, string startingDimension = "MacroSpace")
    {
        var newSave = new SaveProfile
        {
            Id = slotId,
            ProfileName = name,
            CharacterClassId = classId,
            CurrentMapPath = startingMap,
            CurrentDimension = startingDimension,
            LastSaved = DateTime.Now,
            TotalPlayTimeSeconds = 0,
            PersistentWorldMarks = new Dictionary<string, SavedMarkData>()
        };

        CurrentProfile = newSave;
        _ramProfiles[slotId] = newSave;
        HasSaves = true;

        EngineLogger.Log($"[SAVE DISABLED] Profile '{name}' created in RAM.", "SYSTEM");
    }

    public static void LoadProfile(int slotId)
    {
        if (_ramProfiles.TryGetValue(slotId, out var profile))
            CurrentProfile = profile;
        else
            EngineLogger.Log($"Attempted to load non-existent RAM slot {slotId}.", "WARNING");
    }

    public static void SaveToSlot(int slotId, string name, string currentMap, float playerX, float playerY, int currentHp, string currentDimension, float addedPlaytime) { }

    public static void PerformAutoSave(string currentMap, float playerX, float playerY, int currentHp, string currentDimension, float addedPlaytime) { }

    public static void DeleteAllSaves()
    {
        _ramProfiles.Clear();
        CurrentProfile = null;
        HasSaves = false;
        EngineLogger.Log("RAM Saves wiped.", "SYSTEM");
    }
}
