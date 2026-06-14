using System;
using MyGame.Engine.Networking;

namespace MyGame;

public static class Program
{
	[STAThread]
	public static void Main()
	{
		SteamManager.Initialize();

		using var game = new Game1();
		game.Run();
	}
}
