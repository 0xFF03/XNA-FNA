using System;

namespace MyGame.Game.Logic;

public class SaveProfile
{
	public int Id { get; set; }
	public string ProfileName { get; set; } = "Empty Slot";
	public DateTime LastSaved { get; set; }

	// Player State
	public int CharacterClassId { get; set; }
	public int CurrentHealth { get; set; } = 100;

	// World State
	public string CurrentMapPath { get; set; } = "Maps/Level1.ldtk";
	public float CheckpointX { get; set; } = -1;
	public float CheckpointY { get; set; } = -1;
	public string CurrentDimension { get; set; } = "MacroSpace";

	// Meta Progression
	public double TotalPlayTimeSeconds { get; set; } = 0;
}
