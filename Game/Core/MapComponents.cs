using MyGame.Engine.Platform;

namespace MyGame.Game.Core;

public static class MapComponents
{
	public struct MapLoadRequest
	{
		public string MapPath;
		public int LocalClassId;
	}

	public struct MapInstance
	{
		public LevelData Data;
	}
}
