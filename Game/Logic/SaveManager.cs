using System;
using System.Collections.Generic;
using System.Linq;
using Flecs.NET.Core;
using MyGame.Engine.Core;
using MyGame.Game.Core;

namespace MyGame.Game.Logic;

public static class SaveManager
{
    private const string CollectionName = "save_profiles";

    public static SaveProfile? CurrentProfile { get; private set; }
    public static bool HasSaves { get; private set; } = false;

    public static void Initialize()
    {
        var db = Game1.Instance.LocalDatabase;
        var collection = db.GetCollection<SaveProfile>(CollectionName);
        HasSaves = collection.Count() > 0;
    }

    public static SaveProfile? GetLatestProfile()
    {
        var db = Game1.Instance.LocalDatabase;
        var collection = db.GetCollection<SaveProfile>(CollectionName);
        return collection.FindAll().OrderByDescending(x => x.LastSaved).FirstOrDefault();
    }

    public static SaveProfile?[] GetDisplayProfiles()
    {
        var db = Game1.Instance.LocalDatabase;
        var collection = db.GetCollection<SaveProfile>(CollectionName);
        var all = collection.FindAll().ToList();

        var result = new SaveProfile?[10];
        foreach (var p in all)
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
            PersistentWorldMarks = new Dictionary<string, int>()
        };

        CurrentProfile = newSave;
        HasSaves = true;

        var db = Game1.Instance.LocalDatabase;
        var collection = db.GetCollection<SaveProfile>(CollectionName);
        collection.Upsert(newSave);

        EngineLogger.Log($"New profile created and saved to disk: {name}", "SYSTEM");
    }

    public static void LoadProfile(int slotId)
    {
        var db = Game1.Instance.LocalDatabase;
        var collection = db.GetCollection<SaveProfile>(CollectionName);

        CurrentProfile = collection.FindById(slotId);
        if (CurrentProfile == null) EngineLogger.Log($"Attempted to load non-existent save slot {slotId}.", "WARNING");
    }

    public static void SaveToSlot(int slotId, string name, string currentMap, float playerX, float playerY, int currentHp, string currentDimension, float addedPlaytime)
    {
        if (CurrentProfile == null) return;

        CurrentProfile.TotalPlayTimeSeconds += addedPlaytime;

        var savedMarks = new Dictionary<string, int>();

        // ARCHITECTURE FIX: Synchronously collect all modified interaction marks inside the active ECS universe.
        // Other player entities are naturally excluded from serialization.
        var marksQuery = Game1.Instance.EcsWorld.QueryBuilder<WorldMark>().Build();
        marksQuery.Each((ref WorldMark mark) =>
        {
            if (!string.IsNullOrEmpty(mark.UniqueMarkId))
            {
                savedMarks[mark.UniqueMarkId] = mark.InteractionState;
            }
        });

        var newSave = new SaveProfile
        {
            Id = slotId,
            ProfileName = name,
            CharacterClassId = CurrentProfile.CharacterClassId,
            CurrentMapPath = currentMap,
            CheckpointX = playerX,
            CheckpointY = playerY,
            CurrentHealth = currentHp,
            CurrentDimension = currentDimension,
            TotalPlayTimeSeconds = CurrentProfile.TotalPlayTimeSeconds,
            LastSaved = DateTime.Now,
            PersistentWorldMarks = savedMarks
        };

        CurrentProfile = newSave;
        HasSaves = true;

        var db = Game1.Instance.LocalDatabase;
        var collection = db.GetCollection<SaveProfile>(CollectionName);
        collection.Upsert(newSave);

        EngineLogger.Log($"Game manually saved synchronously to slot {slotId}. Saved {savedMarks.Count} structural world modifications.", "SYSTEM");
    }

    public static void PerformAutoSave(string currentMap, float playerX, float playerY, int currentHp, string currentDimension, float addedPlaytime)
    {
        if (CurrentProfile == null) return;
        CurrentProfile.TotalPlayTimeSeconds += addedPlaytime;

        var db = Game1.Instance.LocalDatabase;
        var collection = db.GetCollection<SaveProfile>(CollectionName);

        int oldestSlot = 1;
        DateTime oldestTime = DateTime.MaxValue;

        for (int i = 1; i <= 3; i++)
        {
            var p = collection.FindById(i);
            if (p == null)
            {
                oldestSlot = i;
                break;
            }
            if (p.LastSaved < oldestTime)
            {
                oldestTime = p.LastSaved;
                oldestSlot = i;
            }
        }

        var savedMarks = new Dictionary<string, int>();
        var marksQuery = Game1.Instance.EcsWorld.QueryBuilder<WorldMark>().Build();
        marksQuery.Each((ref WorldMark mark) =>
        {
            if (!string.IsNullOrEmpty(mark.UniqueMarkId))
            {
                savedMarks[mark.UniqueMarkId] = mark.InteractionState;
            }
        });

        var autoSave = new SaveProfile
        {
            Id = oldestSlot,
            ProfileName = $"Auto-Save {oldestSlot}",
            CharacterClassId = CurrentProfile.CharacterClassId,
            CurrentMapPath = currentMap,
            CheckpointX = playerX,
            CheckpointY = playerY,
            CurrentHealth = currentHp,
            CurrentDimension = currentDimension,
            TotalPlayTimeSeconds = CurrentProfile.TotalPlayTimeSeconds,
            LastSaved = DateTime.Now,
            PersistentWorldMarks = savedMarks
        };

        collection.Upsert(autoSave);
        HasSaves = true;

        EngineLogger.Log($"Game auto-saved silently to slot {oldestSlot}", "SYSTEM");
    }

    public static void DeleteAllSaves()
    {
        var db = Game1.Instance.LocalDatabase;
        db.DropCollection(CollectionName);
        CurrentProfile = null;
        HasSaves = false;
        EngineLogger.Log("All save files have been securely wiped.", "SYSTEM");
    }
}
