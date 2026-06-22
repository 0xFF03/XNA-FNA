using System;
using MyGame.Engine.Platform;

namespace MyGame.Engine.Core;

public static class Program
{
	[STAThread]
	public static void Main()
	{
		EngineLogger.Initialize();

		AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
		{
			Exception ex = (Exception)args.ExceptionObject;
			EngineLogger.LogError("FATAL ENGINE CRASH", ex);
			EngineLogger.Shutdown();
		};

#if DEBUG
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
