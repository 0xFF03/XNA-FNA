using System.Threading;
using Steamworks;

namespace MyGame.Engine.Networking;

public static class NetworkIdGenerator
{
	private static uint _currentId = 0;

	public static ulong GetNext()
	{
		ulong accountId = SteamClient.SteamId.AccountId;
		return (accountId << 32) | (++_currentId);
	}

	public static void ResetSequence()
	{
		_currentId = 0;
	}
}
