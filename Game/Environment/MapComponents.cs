using MyGame.Engine.Maps;

namespace MyGame.Game.Environment; // <-- FIXED NAMESPACE

public struct MapLoadRequest
{
	public string MapPath;
	public int LocalClassId;
}

public struct MapInstance
{
	public LevelData Data;
}
