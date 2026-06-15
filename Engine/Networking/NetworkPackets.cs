using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MyGame.Engine.Networking;

public static class PacketTypes
{
    public const byte Transform = 1;
    public const byte Spawn = 2;
    public const byte DynamicString = 3;

    public const byte LobbyStart = 10;
    public const byte PauseGame = 11;
    public const byte ResumeGame = 12;
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
        fixed (byte* ptr = buffer) *(PlayerTransformPacket*)ptr = this;
    }

    public static unsafe PlayerTransformPacket Deserialize(byte[] data)
    {
        if (data.Length < sizeof(PlayerTransformPacket)) return default;
        fixed (byte* ptr = data) return *(PlayerTransformPacket*)ptr;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlayerSpawnPacket
{
    public byte PacketType;
    public int CharacterClassId;
    public float StartX;
    public float StartY;
    public ulong EntityNetworkSequenceId;

    public unsafe void SerializeTo(byte[] buffer)
    {
        fixed (byte* ptr = buffer) *(PlayerSpawnPacket*)ptr = this;
    }

    public static unsafe PlayerSpawnPacket Deserialize(byte[] data)
    {
        if (data.Length < sizeof(PlayerSpawnPacket)) return default;
        fixed (byte* ptr = data) return *(PlayerSpawnPacket*)ptr;
    }
}

public static class DynamicPacketWriter
{
    public static byte[] SerializeStringPacket(byte stringTypeIdentifier, string textPayload)
    {
        byte[] stringBytes = Encoding.UTF8.GetBytes(textPayload);

        byte[] finalPacket = new byte[2 + stringBytes.Length];

        finalPacket[0] = PacketTypes.DynamicString;
        finalPacket[1] = stringTypeIdentifier;

        Buffer.BlockCopy(stringBytes, 0, finalPacket, 2, stringBytes.Length);

        return finalPacket;
    }

    public static string DeserializeStringPacket(byte[] data, out byte stringTypeIdentifier)
    {
        if (data.Length < 2)
        {
            stringTypeIdentifier = 255;
            return string.Empty;
        }

        stringTypeIdentifier = data[1];
        return Encoding.UTF8.GetString(data, 2, data.Length - 2);
    }
}
