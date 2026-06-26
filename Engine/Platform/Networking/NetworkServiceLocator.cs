using MyGame.Engine.Core;

namespace MyGame.Engine.Platform.Networking;

public enum NetworkProviderType
{
	Steam,
	LocalUdp
}

public static class NetworkServiceLocator
{
	private static readonly SteamNetworkService _steamService = new();
	private static readonly UdpNetworkService _udpService = new();

	public static INetworkService Provider { get; private set; } = _steamService;
	public static NetworkProviderType CurrentType { get; private set; } = NetworkProviderType.Steam;

	public static void Initialize(NetworkProviderType defaultType = NetworkProviderType.Steam)
	{
		CurrentType = defaultType;
		Provider = CurrentType == NetworkProviderType.Steam ? _steamService : _udpService;
		Provider.Initialize();
	}

	public static void SwitchProvider(NetworkProviderType newType)
	{
		if (CurrentType == newType) return;

		Provider.Shutdown();
		CurrentType = newType;
		Provider = CurrentType == NetworkProviderType.Steam ? _steamService : _udpService;
		Provider.Initialize();

		EngineLogger.Log($"Network Provider hot-swapped to {newType}", "SYSTEM");
	}

	public static void Update() => Provider.Update();
	public static void Shutdown() => Provider.Shutdown();
}
