using System.Runtime.InteropServices;

namespace MyGame.Gameplay.Networking;

public static class PacketTypes
{
	public const byte Transform = 1;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlayerTransformPacket
{
	public byte PacketType;
	public uint SequenceNumber;
	public int CharacterClassId;
	public float X;
	public float Y;
	public float Vx;
	public float Vy;
	public ulong EntityNetworkSequenceId;

	public unsafe void SerializeTo(byte[] buffer)
	{
		fixed (byte* ptr = buffer)
		{
			*(PlayerTransformPacket*)ptr = this;
		}
	}

	public static unsafe PlayerTransformPacket Deserialize(byte[] data)
	{
		if (data.Length < sizeof(PlayerTransformPacket))
			return default;

		fixed (byte* ptr = data)
		{
			return *(PlayerTransformPacket*)ptr;
		}
	}
}
