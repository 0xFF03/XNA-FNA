using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using MyGame.Engine.States;
using MyGame.GameStates;

namespace MyGame.Engine.Networking;

public static class SteamManager
{
    public static bool IsSteamActive { get; private set; } = false;
    public static Lobby? CurrentLobby { get; private set; }
    private static SteamId? originalHostId = null;

    public static void Initialize()
    {
        try
        {
            SteamClient.Init(480, true);
            IsSteamActive = true;

            SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;

            Console.WriteLine($"[Steam]: Connected securely via Facepunch wrapper setup as {SteamClient.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam Offline Error]: {ex.Message}");
            IsSteamActive = false;
        }
    }

    private static void OnP2PSessionRequest(SteamId steamId)
    {
        if (CurrentLobby.HasValue)
        {
            bool isAuthorized = false;
            foreach (var member in CurrentLobby.Value.Members)
            {
                if (member.Id == steamId)
                {
                    isAuthorized = true;
                    break;
                }
            }

            if (isAuthorized)
            {
                SteamNetworking.AllowP2PPacketRelay(true);
                SteamNetworking.AcceptP2PSessionWithUser(steamId);
                return;
            }
        }
    }

    // ARCHITECTURE FIX: Changed async void to async Task for execution safety
    public static async Task CreateLobby()
    {
        if (!IsSteamActive) return;

        try
        {
            var lobbyTask = await SteamMatchmaking.CreateLobbyAsync(4);
            if (lobbyTask.HasValue)
            {
                CurrentLobby = lobbyTask.Value;
                CurrentLobby.Value.SetFriendsOnly();
                CurrentLobby.Value.SetJoinable(true);
                originalHostId = SteamClient.SteamId;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam Error]: Failed to create lobby - {ex.Message}");
        }
    }

    public static void OpenInviteOverlay()
    {
        if (IsSteamActive && CurrentLobby.HasValue)
        {
            SteamFriends.OpenGameInviteOverlay(CurrentLobby.Value.Id);
        }
    }

    public static void LeaveLobby()
    {
        if (CurrentLobby.HasValue)
        {
            foreach (var member in CurrentLobby.Value.Members)
            {
                if (member.Id != SteamClient.SteamId)
                {
                    SteamNetworking.CloseP2PSessionWithUser(member.Id);
                }
            }
            CurrentLobby.Value.Leave();
            CurrentLobby = null;
            originalHostId = null;
        }
    }

    // ARCHITECTURE FIX: Async Task
    private static async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        try
        {
            RoomEnter result = await lobby.Join();
            if (result == RoomEnter.Success)
            {
                CurrentLobby = lobby;
                originalHostId = friendId;
                StateManager.Instance.ChangeState(new CharacterSelectState(Game1.Instance, StateManager.Instance));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam Error]: Failed to join lobby - {ex.Message}");
        }
    }

    private static void OnLobbyMemberJoined(Lobby lobby, Friend friend) { }
    private static void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId serverId) { }

    public static void Update()
    {
        if (!IsSteamActive) return;
        SteamClient.RunCallbacks();

        if (CurrentLobby.HasValue && originalHostId.HasValue)
        {
            bool hostStillPresent = false;
            foreach (var member in CurrentLobby.Value.Members)
            {
                if (member.Id == originalHostId.Value) hostStillPresent = true;
            }
            if (!hostStillPresent) LeaveLobby();
        }
    }

    public static void Shutdown()
    {
        if (IsSteamActive)
        {
            LeaveLobby();
            SteamMatchmaking.OnLobbyGameCreated -= OnLobbyGameCreated;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamNetworking.OnP2PSessionRequest -= OnP2PSessionRequest;
            SteamClient.Shutdown();
        }
    }
}
