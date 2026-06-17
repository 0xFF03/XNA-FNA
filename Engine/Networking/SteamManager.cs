using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using MyGame.Engine.States;
using MyGame.Engine.Core;
using MyGame.Game.UIStates;

namespace MyGame.Engine.Networking;

public static class SteamManager
{
    public static bool IsSteamActive { get; private set; } = false;
    public static Lobby? CurrentLobby { get; private set; }
    public static SteamId? KnownHostId { get; private set; }
    public static readonly HashSet<ulong> ActiveLobbyMembers = new();

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

    private static void OnP2PSessionRequest(SteamId steamId)
    {
        if (CurrentLobby is { } activeLobby)
        {
            foreach (var member in activeLobby.Members)
            {
                if (member.Id == steamId)
                {
                    SteamNetworking.AllowP2PPacketRelay(true);
                    SteamNetworking.AcceptP2PSessionWithUser(steamId);
                    return;
                }
            }
        }
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
        }
    }

    // FIX: Added missing method
    public static IEnumerable<Friend> GetFriends()
    {
        if (!IsSteamActive) yield break;
        foreach (var friend in SteamFriends.GetFriends()) yield return friend;
    }

    // FIX: Added missing method
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
        }
    }

    private static async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        if (await lobby.Join() == RoomEnter.Success)
        {
            CurrentLobby = lobby;
            KnownHostId = lobby.Owner.Id;
            StateManager.Instance.ChangeState(new CharacterSelectState(Game1.Instance, StateManager.Instance));
        }
    }

    public static void Update()
    {
        if (!IsSteamActive) return;
        SteamClient.RunCallbacks();
        SteamAvatarCache.Update();

        if (CurrentLobby is { } activeLobby && KnownHostId is { } hostId)
        {
            ActiveLobbyMembers.Clear();
            foreach (var member in activeLobby.Members) ActiveLobbyMembers.Add(member.Id.Value);

            if (activeLobby.Owner.Id != hostId) LeaveLobby();
        }
        else ActiveLobbyMembers.Clear();
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
