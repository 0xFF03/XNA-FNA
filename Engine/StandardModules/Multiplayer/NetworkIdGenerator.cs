using System;
using Steamworks;
using MyGame.Engine.Platform;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class NetworkIdGenerator
{
	private static uint _entityCounter = 0;
	private static ulong _offlineSessionSeed = 0;

	public static ulong GetNextNetworkId()
	{
		_entityCounter++;

		if (SteamManager.IsSteamActive)
		{
			ulong accountId = SteamClient.SteamId.AccountId;
			return (accountId << 32) | _entityCounter;
		}

		if (_offlineSessionSeed == 0)
		{
			Random rand = new Random();
			_offlineSessionSeed = (ulong)rand.Next(1, int.MaxValue);
		}

		return (_offlineSessionSeed << 32) | _entityCounter;
	}

	// ARCHITECTURE FIX: Creates identical Network IDs across all clients based on map strings
	public static ulong GetDeterministicNetworkId(string stringSeed)
	{
		if (string.IsNullOrEmpty(stringSeed)) return GetNextNetworkId();

		// FNV-1a Hashing Algorithm to convert string to ulong safely
		ulong hash = 14695981039346656037;
		foreach (char c in stringSeed)
		{
			hash ^= c;
			hash *= 1099511628211;
		}
		return hash;
	}
}
