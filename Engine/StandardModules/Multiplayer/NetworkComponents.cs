namespace MyGame.Engine.StandardModules.Multiplayer;

public enum GameEventType : byte
{
	Damage,
	InteractSwitch,
	Despawn,
	ClaimAuthority
}

public struct NetworkOwner
{
	// ARCHITECTURE FIX: Was SteamId. Now generic ulong. Zero API coupling.
	public ulong Value;
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
