using System.Threading;
using Steamworks;

namespace MyGame.Engine.Networking;

public static class NetworkIdGenerator
{
	private static uint _localCounter = 0;

	// Generates a globally unique 64-bit ID across the entire P2P session
	public static ulong GetNextUniqueId()
	{
		// 1. Grab the user's absolute Steam ID (guaranteed unique globally)
		ulong steamId = SteamClient.SteamId.Value;

		// 2. Safely increment a local counter (Thread-safe)
		uint sequence = (uint)Interlocked.Increment(ref _localCounter);

		// 3. Bit-shifting magic:
		// We take the bottom 32 bits of the Steam ID and shift them high,
		// then pack our local sequence counter into the low 32 bits.
		// This mathematically ensures peer IDs never intersect.
		ulong uniqueNetworkId = (steamId << 32) | sequence;

		return uniqueNetworkId;
	}

	public static void ResetSequence()
	{
		_localCounter = 0;
	}
}
