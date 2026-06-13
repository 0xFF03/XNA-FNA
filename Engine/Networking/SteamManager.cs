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

    public static void Initialize()
    {
        try
        {
            SteamClient.Init(480);
            IsSteamActive = true;

            // Wire up all essential multi-user callback pathways
            SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;

            Console.WriteLine($"[Steam]: Always-Online connected successfully as {SteamClient.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam Offline Error]: Running in fallback mode. {ex.Message}");
            IsSteamActive = false;
        }
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
            // Forces the Steam overlay to jump directly into the friend selection panel for this lobby
            SteamFriends.OpenGameInviteOverlay(CurrentLobby.Value.Id);
            Console.WriteLine($"[Steam]: Summoning explicit Friend Invite overlay for Lobby: {CurrentLobby.Value.Id}");
        }
        else
        {
            Console.WriteLine("[Steam Warning]: Cannot summon invite overlay. Verify you are running an active Steam lobby session.");
        }
    }

    // Fired on the joining client's side when they accept a Steam invite
    private static async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        Console.WriteLine($"[Network]: Accepting lobby link request to join Host ID: {friendId}");

        // FIX 1: Facepunch uses the .Join() invocation signature rather than .JoinAsync()
        RoomEnter result = await lobby.Join();
        if (result == RoomEnter.Success)
        {
            CurrentLobby = lobby;
            Console.WriteLine("[Network]: Connected to the remote lobby successfully!");

            // FIX 2: Target the explicit type-safe desktop assembly window via Game1.Instance
            StateManager.Instance.ChangeState(new CharacterSelectState(Game1.Instance, StateManager.Instance));
        }
        else
        {
            Console.WriteLine($"[Network Error]: Failed to step into target lobby room: {result}");
        }
    }

    // Fired on everyone's machine when a player connects to the lobby session
    private static void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Console.WriteLine($"[Lobby Sync]: Player '{friend.Name}' has successfully stepped into the lobby room.");
    }

    private static void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId serverId)
    {
    }

    public static void Update()
    {
        if (IsSteamActive && SteamClient.IsValid)
        {
            SteamClient.RunCallbacks();
        }
    }

    public static void Shutdown()
    {
        if (IsSteamActive && SteamClient.IsValid)
        {
            SteamClient.Shutdown();
        }
    }
}
