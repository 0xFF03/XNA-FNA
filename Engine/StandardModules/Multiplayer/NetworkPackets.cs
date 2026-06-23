using MemoryPack;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class PacketTypes
{
	public const byte Transform = 1;
	public const byte Spawn = 2;
	public const byte DistributedEvent = 5;
	public const byte ProjectileSpawn = 6;

	public const byte LobbyStart = 10;
	public const byte PauseGame = 11;
	public const byte ResumeGame = 12;
	public const byte PlayerReady = 13;

	public const byte ClientLoadedMap = 14;
	public const byte WorldStateSnapshot = 20;
}

[MemoryPackable]
public partial struct PlayerTransformPacket
{
	public uint SequenceNumber;
	public int CharacterClassId;
	public float X;
	public float Y;
	public float Vx;
	public float Vy;
	public int FacingDirection;
	public ulong EntityNetworkSequenceId;
	public string TargetPhysicsWorld;
}

[MemoryPackable]
public partial struct PlayerSpawnPacket
{
	public int CharacterClassId;
	public float StartX;
	public float StartY;
	public ulong EntityNetworkSequenceId;
	public string TargetPhysicsWorld;
}

[MemoryPackable]
public partial struct DistributedEventPacket
{
	public ulong TargetNetworkId;
	public byte EventType;
	public int IntPayload;
	public float FloatPayload;
	public ulong UlongPayload; // Handles 64-bit Steam ID Authority swaps securely
}

[MemoryPackable]
public partial struct ProjectileSpawnPacket
{
	public float StartX;
	public float StartY;
	public float VelocityX;
	public float VelocityY;
	public ulong EntityNetworkSequenceId;
	public ulong OwnerSteamId;
	public string TargetPhysicsWorld;
}

[MemoryPackable]
public partial struct EntitySnapshot
{
	public ulong NetworkId;
	public byte EntityType;
	public float X;
	public float Y;
	public int Health;
	public int FacingDirection;
	public ulong OwnerSteamId;
	public string TargetPhysicsWorld;
}

[MemoryPackable]
public partial struct WorldStatePacket
{
	public EntitySnapshot[] Entities;
}
