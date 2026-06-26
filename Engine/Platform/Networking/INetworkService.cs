using System.Collections.Generic;

namespace MyGame.Engine.Platform.Networking;

public struct NetworkPacket
{
	public ulong SenderId;
	public byte[] Data;
	public int Length;
}

public struct NetworkFriend
{
	public ulong Id;
	public string Name;
	public bool IsOnline;
}

public interface INetworkService
{
	bool IsActive { get; }
	bool IsInLobby { get; }
	ulong LocalUserId { get; }
	ulong? HostId { get; }

	HashSet<ulong> ActivePeers { get; }
	Dictionary<ulong, string> PeerNames { get; }

	void Initialize();
	void Update();
	void Shutdown();

	void CreateLobby();
	void JoinLobby(string connectionString);
	void LeaveLobby();

	IEnumerable<NetworkFriend> GetFriends();
	void InviteFriend(ulong friendId);

	void SendPacket(ulong targetId, byte[] data, int length, byte channel, bool reliable);
	void BroadcastPacket(byte[] data, int length, byte channel, bool reliable);
	bool TryReadPacket(byte channel, out NetworkPacket packet);
}
