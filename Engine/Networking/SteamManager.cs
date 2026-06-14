using System;
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

            Console.WriteLine($"[Steam]: Always-Online connected successfully as {SteamClient.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam Offline Error]: Running in fallback mode. {ex.Message}");
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
                Console.WriteLine($"[Steam]: Authorized P2P handshake accepted from {steamId}");
                SteamNetworking.AcceptP2PSessionWithUser(steamId);
                return;
            }
        }

        Console.WriteLine($"[Steam Security]: BLOCKED unauthorized P2P request from {steamId}");
    }

    public static async void CreateLobby()
    {
        if (!IsSteamActive || !SteamClient.IsValid) return;

        try
        {
            var lobbyTask = await SteamMatchmaking.CreateLobbyAsync(4);
            if (lobbyTask.HasValue)
            {
                CurrentLobby = lobbyTask.Value;
                CurrentLobby.Value.SetFriendsOnly();
                CurrentLobby.Value.SetJoinable(true);
                originalHostId = SteamClient.SteamId;
                Console.WriteLine($"[Steam]: Live Multiplayer Lobby Formed! ID: {CurrentLobby.Value.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam Lobby Error]: Failed to construct network instance. {ex.Message}");
        }
    }

    public static void OpenInviteOverlay()
    {
        if (IsSteamActive && SteamClient.IsValid && CurrentLobby.HasValue)
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
            Console.WriteLine("[Steam]: Safely severed all P2P sockets and left the lobby.");
        }
    }

    private static async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        RoomEnter result = await lobby.Join();
        if (result == RoomEnter.Success)
        {
            CurrentLobby = lobby;
            originalHostId = friendId;
            Console.WriteLine("[Network]: Connected to the remote lobby successfully!");
            StateManager.Instance.ChangeState(new CharacterSelectState(Game1.Instance, StateManager.Instance));
        }
    }

    private static void OnLobbyMemberJoined(Lobby lobby, Friend friend) { }
    private static void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId serverId) { }

    public static void Update()
    {
        if (IsSteamActive && SteamClient.IsValid)
        {
            SteamClient.RunCallbacks();

            if (CurrentLobby.HasValue && originalHostId.HasValue)
            {
                bool hostStillPresent = false;
                foreach (var member in CurrentLobby.Value.Members)
                {
                    if (member.Id == originalHostId.Value)
                    {
                        hostStillPresent = true;
                        break;
                    }
                }

                if (!hostStillPresent)
                {
                    Console.WriteLine("[Network Sync]: Critical Authority Lost - The original host left the lobby.");
                    LeaveLobby();
                }
            }
        }
    }

    public static void Shutdown()
    {
        if (IsSteamActive && SteamClient.IsValid)
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
