using System;
using MyGame.Engine.Networking;

namespace MyGame;

public static class Program
{
	[STAThread]
	public static void Main()
	{
		// CRITICAL FIX: Steam must initialize before the FNA graphics device exists
		// so it can successfully hook the Vulkan rendering swapchain.
		SteamManager.Initialize();

		using var game = new Game1();
		game.Run();
	}
}
