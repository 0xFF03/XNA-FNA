using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using MyGame.Engine.Platform;
using MyGame.Engine.Core;
using MyGame.Game.UIStates;

namespace MyGame.Engine.Platform;

public static class SteamManager
{
    public static bool IsSteamActive { get; private set; } = false;
    public static Lobby? CurrentLobby { get; private set; }
    public static SteamId? KnownHostId { get; private set; }

    public static readonly HashSet<ulong> ActiveLobbyMembers = new();

    // ARCHITECTURE FIX: Caches strings once per second. Zero-allocation UI rendering.
    public static readonly Dictionary<ulong, string> ActiveLobbyNames = new();

    private static DateTime _lastLobbyPoll = DateTime.MinValue;

    public static void Initialize()
    {
        try
        {
            SteamClient.Init(480, true);
            IsSteamActive = true;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
            SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;
            EngineLogger.Log($"Steam Client initialized: {SteamClient.Name}", "STEAM");
        }
        catch (Exception ex)
        {
            EngineLogger.Log($"Steam init failed: {ex.Message}", "ERROR");
            IsSteamActive = false;
        }
    }

    // ARCHITECTURE FIX: Safely called by Game1 only AFTER StateManager is fully constructed.
    public static void CheckCommandLineInvites()
    {
        if (!IsSteamActive) return;

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

    public static ulong GetLocalOrHostId()
    {
        if (KnownHostId.HasValue) return KnownHostId.Value.Value;
        if (IsSteamActive) return SteamClient.SteamId.Value;
        return 0;
    }

    private static void OnP2PSessionRequest(SteamId steamId)
    {
        SteamNetworking.AcceptP2PSessionWithUser(steamId);
    }

    public static async Task CreateLobby()
    {
        if (!IsSteamActive) return;
        var lobby = await SteamMatchmaking.CreateLobbyAsync(4);
        if (lobby.HasValue)
        {
            CurrentLobby = lobby.Value;
            CurrentLobby.Value.SetFriendsOnly();
            KnownHostId = SteamClient.SteamId;
            CurrentLobby.Value.SetData("GameState", "Lobby");
            ForceRosterUpdate();
        }
    }

    private static async void JoinLobbyFromCommandLineAsync(ulong lobbyId)
    {
        var lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
        if (lobby.HasValue)
        {
            CurrentLobby = lobby.Value;
            KnownHostId = lobby.Value.Owner.Id;
            ForceRosterUpdate();
            StateManager.Instance.ChangeState(new CharacterSelectState(Game1.Instance, StateManager.Instance));
        }
    }

    private static async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        if (await lobby.Join() == RoomEnter.Success)
        {
            CurrentLobby = lobby;
            KnownHostId = lobby.Owner.Id;
            ForceRosterUpdate();
            StateManager.Instance.ChangeState(new CharacterSelectState(Game1.Instance, StateManager.Instance));
        }
    }

    public static IEnumerable<Friend> GetFriends()
    {
        if (!IsSteamActive) yield break;
        foreach (var friend in SteamFriends.GetFriends()) yield return friend;
    }

    public static void InviteFriendToLobby(SteamId friendId)
    {
        if (IsSteamActive && CurrentLobby.HasValue) CurrentLobby.Value.InviteFriend(friendId);
    }

    public static void LeaveLobby()
    {
        if (CurrentLobby is { } activeLobby)
        {
            foreach (var member in activeLobby.Members)
            {
                if (member.Id != SteamClient.SteamId) SteamNetworking.CloseP2PSessionWithUser(member.Id);
            }
            activeLobby.Leave();
            CurrentLobby = null;
            KnownHostId = null;
            ActiveLobbyMembers.Clear();
            ActiveLobbyNames.Clear();
        }
    }

    private static void ForceRosterUpdate()
    {
        if (CurrentLobby is { } activeLobby)
        {
            ActiveLobbyMembers.Clear();
            ActiveLobbyNames.Clear();
            foreach (var member in activeLobby.Members)
            {
                ActiveLobbyMembers.Add(member.Id.Value);
                ActiveLobbyNames[member.Id.Value] = member.Name;
            }
        }
    }

    public static void Update()
    {
        if (!IsSteamActive) return;
        SteamClient.RunCallbacks();
        SteamAvatarCache.Update();

        if ((DateTime.UtcNow - _lastLobbyPoll).TotalSeconds > 1.0)
        {
            _lastLobbyPoll = DateTime.UtcNow;

            if (CurrentLobby is { } activeLobby && KnownHostId is { } hostId)
            {
                ActiveLobbyMembers.Clear();
                ActiveLobbyNames.Clear();
                bool hostStillPresent = false;

                foreach (var member in activeLobby.Members)
                {
                    ActiveLobbyMembers.Add(member.Id.Value);
                    ActiveLobbyNames[member.Id.Value] = member.Name;
                    if (member.Id == hostId) hostStillPresent = true;
                }

                if (!hostStillPresent) LeaveLobby();
            }
            else
            {
                ActiveLobbyMembers.Clear();
                ActiveLobbyNames.Clear();
            }
        }
    }

    public static void Shutdown()
    {
        if (IsSteamActive)
        {
            LeaveLobby();
            SteamClient.Shutdown();
        }
    }
}
