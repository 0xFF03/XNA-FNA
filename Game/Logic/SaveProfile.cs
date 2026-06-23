using System;
using System.Collections.Generic;

namespace MyGame.Game.Logic;

// ARCHITECTURE FIX: Tracks dynamic coordinates for moved vehicles or pushed objects
public struct SavedMarkData
{
	public int State { get; set; }
	public float X { get; set; }
	public float Y { get; set; }
}

public class SaveProfile
{
	public int Id { get; set; }
	public string ProfileName { get; set; } = "Empty Slot";
	public DateTime LastSaved { get; set; }

	public int CharacterClassId { get; set; }
	public int CurrentHealth { get; set; } = 100;

	public string CurrentMapPath { get; set; } = "Maps/Level1.ldtk";
	public float CheckpointX { get; set; } = -1;
	public float CheckpointY { get; set; } = -1;
	public string CurrentDimension { get; set; } = "MacroSpace";

	public double TotalPlayTimeSeconds { get; set; } = 0;

	public Dictionary<string, SavedMarkData> PersistentWorldMarks { get; set; } = new();
}
