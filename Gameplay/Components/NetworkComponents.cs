using Steamworks;

namespace MyGame.Gameplay.Components;

public struct NetworkOwner
{
	public SteamId Value;
}

public struct NetworkId
{
	public ulong Value;
}

public struct NetworkSequence
{
	public uint LatestSequence;
}
