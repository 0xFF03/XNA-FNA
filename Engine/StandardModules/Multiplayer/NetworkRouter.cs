using System;
using Steamworks;
using MyGame.Engine.Platform;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class NetworkRouter
{
	public static event Action? OnLobbyMatchStart;
	public static event Action<SteamId>? OnJoineeReady;
	public static event Action<SteamId>? OnClientLoadedMap;

	// ARCHITECTURE FIX: Event now carries sender ID to notify UI who paused the game
	public static event Action<bool, SteamId>? OnPauseStateChanged;

	public static void RouteControlPackets()
	{
		if (!SteamManager.IsSteamActive) return;

		while (SteamNetworking.IsP2PPacketAvailable(2))
		{
			var packetData = SteamNetworking.ReadP2PPacket(2);
			if (packetData.HasValue && packetData.Value.Data.Length > 0)
			{
				byte signal = packetData.Value.Data[0];
				SteamId senderId = packetData.Value.SteamId;

				switch (signal)
				{
					case PacketTypes.LobbyStart:
						OnLobbyMatchStart?.Invoke();
						break;
					case PacketTypes.PlayerReady:
						OnJoineeReady?.Invoke(senderId);
						break;
					case PacketTypes.ClientLoadedMap:
						OnClientLoadedMap?.Invoke(senderId);
						break;
					case PacketTypes.PauseGame:
						OnPauseStateChanged?.Invoke(true, senderId);
						break;
					case PacketTypes.ResumeGame:
						OnPauseStateChanged?.Invoke(false, senderId);
						break;
				}
			}
		}
	}
}
