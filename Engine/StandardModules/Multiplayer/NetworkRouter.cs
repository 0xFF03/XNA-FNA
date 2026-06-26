using System;
using MyGame.Engine.Core;
using MyGame.Engine.Platform.Networking;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class NetworkRouter
{
    public static event Action? OnLobbyMatchStart;
    public static event Action<ulong>? OnJoineeReady;
    public static event Action<ulong>? OnClientLoadedMap;
    public static event Action<bool, ulong>? OnPauseStateChanged;

    public static void RouteControlPackets()
    {
        var net = NetworkServiceLocator.Provider;
        if (!net.IsActive) return;

        while (net.TryReadPacket(2, out var packet))
        {
            if (packet.Length > 0)
            {
                byte signal = packet.Data[0];
                ulong senderId = packet.SenderId;

                switch (signal)
                {
                    case PacketTypes.LobbyStart:
                        EngineLogger.Log("Received LobbyStart control packet.", "NETWORK");
                        OnLobbyMatchStart?.Invoke();
                        break;
                    case PacketTypes.PlayerReady:
                        EngineLogger.Log($"Received PlayerReady handshake from {senderId}.", "NETWORK");
                        OnJoineeReady?.Invoke(senderId);
                        break;
                    case PacketTypes.ClientLoadedMap:
                        EngineLogger.Log($"Received ClientLoadedMap handshake from {senderId}.", "NETWORK");
                        OnClientLoadedMap?.Invoke(senderId);
                        break;
                    case PacketTypes.PauseGame:
                        EngineLogger.Log($"Received PauseGame command from {senderId}.", "NETWORK");
                        OnPauseStateChanged?.Invoke(true, senderId);
                        break;
                    case PacketTypes.ResumeGame:
                        EngineLogger.Log($"Received ResumeGame command from {senderId}.", "NETWORK");
                        OnPauseStateChanged?.Invoke(false, senderId);
                        break;
                    default:
                        EngineLogger.Log($"Received unknown Channel 2 control packet: {signal}", "WARNING");
                        break;
                }
            }
        }
    }
}
