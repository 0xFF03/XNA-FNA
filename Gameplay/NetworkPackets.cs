using System.Runtime.InteropServices;

namespace MyGame.Gameplay;

// Pack=1 ensures the C# compiler doesn't add empty padding bytes between variables
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlayerTransformPacket
{
	public byte PacketType;
	public int CharacterClassId;
	public float X;
	public float Y;
	public float Vx;
	public float Vy;

	// Zero-allocation serialization straight into a pre-existing byte array buffer
	public unsafe void SerializeTo(byte[] buffer)
	{
		fixed (byte* ptr = buffer)
		{
			*(PlayerTransformPacket*)ptr = this;
		}
	}

	// Zero-allocation deserialization reading straight off the incoming packet memory
	public static unsafe PlayerTransformPacket Deserialize(byte[] data)
	{
		fixed (byte* ptr = data)
		{
			return *(PlayerTransformPacket*)ptr;
		}
	}
}
