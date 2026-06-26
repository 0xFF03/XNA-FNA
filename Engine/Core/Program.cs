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
			// ARCHITECTURE FIX: Bypasses the async queue to guarantee the error hits the disk
			EngineLogger.LogFatalSync("FATAL ENGINE CRASH", ex);
			EngineLogger.Shutdown();
			Environment.Exit(1); // Prevents unresponsive ghost windows
		};

#if DEBUG
		AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
		{
			// First chance exceptions are normal in .NET (e.g. handled try/catch), only log them to console, not disk
			Console.WriteLine($"[DIAGNOSTIC] FirstChanceException: {eventArgs.Exception.Message}");
		};
#endif

		using var game = new Game1();
		game.Run();
	}
}
