using System.Collections.Generic;

namespace MyGame.Game.Registries;

public struct ClassDefinition
{
    public int Id { get; set; }
    public string ClassName { get; set; }

    // Base Stats
    public int BaseHealth { get; set; }
    public float MovementSpeed { get; set; }
    public float JumpForce { get; set; }

    // Asset Paths for PixelOver Integration
    public string SkeletonPath { get; set; }
    public string AtlasTexturePath { get; set; }
}

public static class ClassRegistry
{
    private static readonly Dictionary<int, ClassDefinition> _classes = new();

    // In a full production game, you would parse this from a JSON file.
    // For now, we statically initialize the class definitions here.
    static ClassRegistry()
    {
        _classes[0] = new ClassDefinition
        {
            Id = 0,
            ClassName = "Vanguard",
            BaseHealth = 150,
            MovementSpeed = 8f,   // Standard speed
            JumpForce = -12f,     // Standard jump
            SkeletonPath = "Skeletons/Vanguard.json",
            AtlasTexturePath = "Textures/Vanguard_Atlas.png"
        };

        _classes[1] = new ClassDefinition
        {
            Id = 1,
            ClassName = "Mage",
            BaseHealth = 80,
            MovementSpeed = 6.5f, // Slower
            JumpForce = -14f,     // Higher, floatier jump
            SkeletonPath = "Skeletons/Mage.json",
            AtlasTexturePath = "Textures/Mage_Atlas.png"
        };

        _classes[2] = new ClassDefinition
        {
            Id = 2,
            ClassName = "Ranger",
            BaseHealth = 100,
            MovementSpeed = 10f,  // Faster
            JumpForce = -11f,     // Shorter, snappier jump
            SkeletonPath = "Skeletons/Ranger.json",
            AtlasTexturePath = "Textures/Ranger_Atlas.png"
        };
    }

    public static ClassDefinition GetClass(int id)
    {
        if (_classes.TryGetValue(id, out var definition)) return definition;

        // Fallback to default class if ID is unknown
        Engine.Core.EngineLogger.Log($"Requested invalid Class ID {id}. Defaulting to Vanguard.", "WARNING");
        return _classes[0];
    }
}
