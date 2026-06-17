using Steamworks;

namespace MyGame.Engine.Networking;

public static class NetworkIdGenerator
{
	private static uint _projectileCounter = 0;

	public static ulong GetNextProjectileId()
	{
		_projectileCounter++;

		ulong accountId = SteamClient.SteamId.AccountId;
		return (accountId << 32) | _projectileCounter;
	}
}
