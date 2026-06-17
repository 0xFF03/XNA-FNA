using Steamworks;

namespace MyGame.Game.NetworkSync;

public enum GameEventType : byte
{
	Damage,
	InteractSwitch,
	Despawn
}

public struct OutboundDistributedEvent
{
	public ulong TargetNetworkId;
	public byte EventType;
	public int IntPayload;
	public float FloatPayload;
}

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
	public float TimeSinceLastPacket;
}
