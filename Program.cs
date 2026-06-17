using System;
using MyGame.Engine.Networking;
using MyGame.Engine.Core;

namespace MyGame;

public static class Program
{
	[STAThread]
	public static void Main()
	{
		EngineLogger.Initialize();

		// Catches hard fatal crashes
		AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
		{
			Exception ex = (Exception)args.ExceptionObject;
			EngineLogger.LogError("FATAL ENGINE CRASH", ex);
			EngineLogger.Shutdown();
		};

#if DEBUG
		// ARCHITECTURE FIX: Gated behind DEBUG mode.
		// FirstChanceException triggers on safely handled internal library exceptions.
		// Running this in a Release build on Linux/Proton will cause massive I/O lag spikes.
		AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
		{
			EngineLogger.Log($"FirstChanceException detected: {eventArgs.Exception.Message}", "DIAGNOSTIC");
		};
#endif

		SteamManager.Initialize();

		using var game = new Game1();
		game.Run();
	}
}
