using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using MyGame.Engine.Core;
using MyGame.Game.UIStates;

namespace MyGame.Engine.Platform.Networking;

public class SteamNetworkService : INetworkService
{
    public bool IsActive { get; private set; } = false;
    public bool IsInLobby => CurrentLobby.HasValue;
    public ulong LocalUserId => IsActive ? SteamClient.SteamId.Value : 0;
    public ulong? HostId { get; private set; }

    public HashSet<ulong> ActivePeers { get; } = new();
    public Dictionary<ulong, string> PeerNames { get; } = new();

    public Lobby? CurrentLobby { get; private set; }
    private DateTime _lastLobbyPoll = DateTime.MinValue;

    public void Initialize()
    {
        try
        {
            SteamClient.Init(480, true);
            IsActive = true;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
            SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;
            EngineLogger.Log($"Steam Service initialized: {SteamClient.Name}", "STEAM");

            CheckCommandLineInvites();
        }
        catch (Exception ex)
        {
            EngineLogger.Log($"Steam init failed: {ex.Message}", "ERROR");
            IsActive = false;
        }
    }

    private void CheckCommandLineInvites()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "+connect_lobby" && i + 1 < args.Length)
            {
                if (ulong.TryParse(args[i + 1], out ulong lobbyId))
                {
                    JoinLobbyFromCommandLineAsync(lobbyId);
                }
            }
        }
    }

    private void OnP2PSessionRequest(SteamId steamId)
    {
        SteamNetworking.AcceptP2PSessionWithUser(steamId);
    }

    public async void CreateLobby()
    {
        if (!IsActive) return;
        var lobby = await SteamMatchmaking.CreateLobbyAsync(4);
        if (lobby.HasValue)
        {
            CurrentLobby = lobby.Value;
            CurrentLobby.Value.SetFriendsOnly();
            HostId = LocalUserId;
            CurrentLobby.Value.SetData("GameState", "Lobby");
            ForceRosterUpdate();
        }
    }

    public async void JoinLobby(string connectionString)
    {
        if (ulong.TryParse(connectionString, out ulong lobbyId))
        {
            var lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
            if (lobby.HasValue)
            {
                CurrentLobby = lobby.Value;
                HostId = lobby.Value.Owner.Id.Value;
                ForceRosterUpdate();
            }
        }
    }

    private async void JoinLobbyFromCommandLineAsync(ulong lobbyId)
    {
        var lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
        if (lobby.HasValue)
        {
            CurrentLobby = lobby.Value;
            HostId = lobby.Value.Owner.Id.Value;
            ForceRosterUpdate();
            StateManager.Instance.ChangeState(new CharacterSelectState(Game1.Instance, StateManager.Instance));
        }
    }

    private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        if (await lobby.Join() == RoomEnter.Success)
        {
            CurrentLobby = lobby;
            HostId = lobby.Owner.Id.Value;
            ForceRosterUpdate();
            StateManager.Instance.ChangeState(new CharacterSelectState(Game1.Instance, StateManager.Instance));
        }
    }

    public IEnumerable<NetworkFriend> GetFriends()
    {
        if (!IsActive) yield break;
        foreach (var friend in SteamFriends.GetFriends())
        {
            yield return new NetworkFriend
            {
                Id = friend.Id.Value,
                Name = friend.Name,
                IsOnline = friend.IsOnline
            };
        }
    }

    public void InviteFriend(ulong friendId)
    {
        if (IsActive && CurrentLobby.HasValue)
            CurrentLobby.Value.InviteFriend(friendId);
    }

    public void LeaveLobby()
    {
        if (CurrentLobby is { } activeLobby)
        {
            foreach (var member in activeLobby.Members)
            {
                if (member.Id != SteamClient.SteamId) SteamNetworking.CloseP2PSessionWithUser(member.Id);
            }
            activeLobby.Leave();
            CurrentLobby = null;
            HostId = null;
            ActivePeers.Clear();
            PeerNames.Clear();
        }
    }

    private void ForceRosterUpdate()
    {
        if (CurrentLobby is { } activeLobby)
        {
            ActivePeers.Clear();
            PeerNames.Clear();
            foreach (var member in activeLobby.Members)
            {
                ActivePeers.Add(member.Id.Value);
                PeerNames[member.Id.Value] = member.Name;
            }
        }
    }

    public void SendPacket(ulong targetId, byte[] data, int length, byte channel, bool reliable)
    {
        P2PSend sendType = reliable ? P2PSend.Reliable : P2PSend.Unreliable;
        SteamNetworking.SendP2PPacket(targetId, data, length, channel, sendType);
    }

    public void BroadcastPacket(byte[] data, int length, byte channel, bool reliable)
    {
        P2PSend sendType = reliable ? P2PSend.Reliable : P2PSend.Unreliable;
        ulong localId = LocalUserId;
        foreach (var memberId in ActivePeers)
        {
            if (memberId != localId)
            {
                SteamNetworking.SendP2PPacket(memberId, data, length, channel, sendType);
            }
        }
    }

    public bool TryReadPacket(byte channel, out NetworkPacket packet)
    {
        packet = default;
        if (!IsActive) return false;

        if (SteamNetworking.IsP2PPacketAvailable(channel))
        {
            var p = SteamNetworking.ReadP2PPacket(channel);
            if (p.HasValue)
            {
                packet = new NetworkPacket
                {
                    SenderId = p.Value.SteamId.Value,
                    Data = p.Value.Data,
                    Length = p.Value.Data.Length
                };
                return true;
            }
        }
        return false;
    }

    public void Update()
    {
        if (!IsActive) return;
        SteamClient.RunCallbacks();
        SteamAvatarCache.Update();

        if ((DateTime.UtcNow - _lastLobbyPoll).TotalSeconds > 1.0)
        {
            _lastLobbyPoll = DateTime.UtcNow;

            if (CurrentLobby is { } activeLobby && HostId.HasValue)
            {
                ActivePeers.Clear();
                PeerNames.Clear();
                bool hostStillPresent = false;

                foreach (var member in activeLobby.Members)
                {
                    ActivePeers.Add(member.Id.Value);
                    PeerNames[member.Id.Value] = member.Name;
                    if (member.Id.Value == HostId.Value) hostStillPresent = true;
                }

                if (!hostStillPresent) LeaveLobby();
            }
            else
            {
                ActivePeers.Clear();
                PeerNames.Clear();
            }
        }
    }

    public void Shutdown()
    {
        if (IsActive)
        {
            LeaveLobby();
            SteamAvatarCache.Clear();
            SteamClient.Shutdown();
        }
    }
}
