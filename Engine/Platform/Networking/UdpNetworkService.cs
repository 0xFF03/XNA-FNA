using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using MyGame.Engine.Core;

namespace MyGame.Engine.Platform.Networking;

public class UdpNetworkService : INetworkService
{
    public bool IsActive { get; private set; } = false;
    public bool IsInLobby => HostId.HasValue;
    public ulong LocalUserId { get; private set; }
    public ulong? HostId { get; private set; }
    public HashSet<ulong> ActivePeers { get; } = new();
    public Dictionary<ulong, string> PeerNames { get; } = new();

    public static float SimulatedPacketLossPercent = 0.0f;
    public static int SimulatedLatencyMs = 0;
    public static int SimulatedJitterMs = 0;

    private UdpClient? _udpClient;
    private IPEndPoint? _hostEndPoint;
    private readonly Dictionary<ulong, IPEndPoint> _peerEndpoints = new();

    private struct DelayedPacket
    {
        public bool IsActive;
        public DateTime DeliveryTime;
        public byte[] Data;
        public int Length;
        public byte Channel;
        public ulong ExplicitSenderId;
        public IPEndPoint Source;
    }

    private readonly DelayedPacket[] _delayBuffer = new DelayedPacket[2048];
    private int _delayHead = 0;
    private readonly Random _rand = new();

    [ThreadStatic] private static byte[]? _wireBuffer;

    private readonly byte[] _receiveBuffer = new byte[65535];
    private EndPoint _reusableEndpoint = new IPEndPoint(IPAddress.Any, 0);

    public void Initialize()
    {
        try
        {
            int port = 7777;
            try { _udpClient = new UdpClient(port); LocalUserId = 7777; }
            catch { port = 7778; _udpClient = new UdpClient(port); LocalUserId = 7778; }

            const int SIO_UDP_CONNRESET = -1744830452;
            try { _udpClient.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null); } catch { }

            _udpClient.Client.Blocking = false;
            IsActive = true;
            EngineLogger.Log($"[UDP Service] Active on Port {port}. UserID: {LocalUserId}", "NETWORK");

            for (int i = 0; i < _delayBuffer.Length; i++) _delayBuffer[i].Data = new byte[2048];
        }
        catch (Exception ex)
        {
            EngineLogger.LogFatalSync("Local UDP Init Failed", ex);
        }
    }

    public void CreateLobby()
    {
        HostId = LocalUserId;
        ActivePeers.Add(LocalUserId);
        PeerNames[LocalUserId] = "Local Host";
        EngineLogger.Log("[UDP Service] Local Lobby Hosted.", "NETWORK");
    }

    public void JoinLobby(string connectionString)
    {
        try
        {
            string[] parts = connectionString.Split(':');
            _hostEndPoint = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));

            HostId = (ulong)_hostEndPoint.Port;
            ActivePeers.Add(HostId.Value);
            PeerNames[HostId.Value] = "Host";

            ActivePeers.Add(LocalUserId);
            PeerNames[LocalUserId] = "Local Client";

            byte[] handshake = { 13 }; // PacketTypes.PlayerReady

            _wireBuffer ??= new byte[65535];
            _wireBuffer[0] = 2;

            BitConverter.TryWriteBytes(new Span<byte>(_wireBuffer, 1, 8), LocalUserId);
            Buffer.BlockCopy(handshake, 0, _wireBuffer, 9, handshake.Length);

            _udpClient!.Send(_wireBuffer, handshake.Length + 9, _hostEndPoint);
            EngineLogger.Log($"[UDP Service] Joining Host at {_hostEndPoint}", "NETWORK");
        }
        catch (Exception ex)
        {
            EngineLogger.LogError($"[UDP Service] Failed to parse IP or send Handshake to {connectionString}", ex);
        }
    }

    public void Update()
    {
        if (!IsActive || _udpClient == null) return;

        try
        {
            while (_udpClient.Available > 0)
            {
                int receivedBytes = _udpClient.Client.ReceiveFrom(_receiveBuffer, ref _reusableEndpoint);

                if (!IsInLobby) continue;

                if (_rand.NextDouble() < SimulatedPacketLossPercent)
                {
                    EngineLogger.Log($"[UDP SIMULATOR] Packet dropped from {((IPEndPoint)_reusableEndpoint).Port}", "NETWORK");
                    continue;
                }

                if (receivedBytes < 9) continue;

                int delay = SimulatedLatencyMs + _rand.Next(-SimulatedJitterMs, SimulatedJitterMs);
                if (delay < 0) delay = 0;

                byte channel = _receiveBuffer[0];
                ulong explicitSenderId = BitConverter.ToUInt64(_receiveBuffer, 1);
                int payloadLength = receivedBytes - 9;

                int nextIdx = (_delayHead + 1) % _delayBuffer.Length;
                _delayBuffer[nextIdx].IsActive = true;
                _delayBuffer[nextIdx].DeliveryTime = DateTime.UtcNow.AddMilliseconds(delay);
                _delayBuffer[nextIdx].Length = payloadLength;
                _delayBuffer[nextIdx].Channel = channel;
                _delayBuffer[nextIdx].ExplicitSenderId = explicitSenderId;
                _delayBuffer[nextIdx].Source = (IPEndPoint)_reusableEndpoint;

                Buffer.BlockCopy(_receiveBuffer, 9, _delayBuffer[nextIdx].Data, 0, payloadLength);

                _delayHead = nextIdx;
            }
        }
        catch (SocketException) { }
    }

    public bool TryReadPacket(byte channel, out NetworkPacket packet)
    {
        packet = default;
        DateTime now = DateTime.UtcNow;

        for (int i = 0; i < _delayBuffer.Length; i++)
        {
            if (_delayBuffer[i].IsActive && _delayBuffer[i].Channel == channel && _delayBuffer[i].DeliveryTime <= now)
            {
                ulong senderId = _delayBuffer[i].ExplicitSenderId;

                if (!_peerEndpoints.ContainsKey(senderId))
                {
                    _peerEndpoints[senderId] = _delayBuffer[i].Source;
                    ActivePeers.Add(senderId);
                    PeerNames[senderId] = $"Peer_{senderId}";
                    EngineLogger.Log($"[UDP Service] Discovered new authenticated peer: {senderId}", "NETWORK");
                }

                packet = new NetworkPacket
                {
                    SenderId = senderId,
                    Data = _delayBuffer[i].Data,
                    Length = _delayBuffer[i].Length
                };

                _delayBuffer[i].IsActive = false;
                return true;
            }
        }
        return false;
    }

    public void BroadcastPacket(byte[] data, int length, byte channel, bool reliable)
    {
        _wireBuffer ??= new byte[65535];
        _wireBuffer[0] = channel;
        BitConverter.TryWriteBytes(new Span<byte>(_wireBuffer, 1, 8), LocalUserId);
        Buffer.BlockCopy(data, 0, _wireBuffer, 9, length);

        if (_hostEndPoint != null) _udpClient?.Send(_wireBuffer, length + 9, _hostEndPoint);
        else
        {
            foreach (var ep in _peerEndpoints.Values) _udpClient?.Send(_wireBuffer, length + 9, ep);
        }
    }

    public void SendPacket(ulong targetId, byte[] data, int length, byte channel, bool reliable)
    {
        _wireBuffer ??= new byte[65535];
        _wireBuffer[0] = channel;
        BitConverter.TryWriteBytes(new Span<byte>(_wireBuffer, 1, 8), LocalUserId);
        Buffer.BlockCopy(data, 0, _wireBuffer, 9, length);

        if (_peerEndpoints.TryGetValue(targetId, out var ep)) _udpClient?.Send(_wireBuffer, length + 9, ep);
        else if (targetId == HostId && _hostEndPoint != null) _udpClient?.Send(_wireBuffer, length + 9, _hostEndPoint);
    }

    public IEnumerable<NetworkFriend> GetFriends() => Array.Empty<NetworkFriend>();
    public void InviteFriend(ulong friendId) { EngineLogger.Log($"[UDP Service] Mock invite sent to UDP peer {friendId}.", "NETWORK"); }

    public void LeaveLobby()
    {
        EngineLogger.Log("[UDP Service] Left Lobby. Clearing peers.", "NETWORK");
        HostId = null;
        ActivePeers.Clear();
        _peerEndpoints.Clear();

        for (int i = 0; i < _delayBuffer.Length; i++) _delayBuffer[i].IsActive = false;

        // ARCHITECTURE FIX: Completely flushes OS-level ghosts from the socket buffer on disconnect
        if (_udpClient != null)
        {
            try
            {
                while (_udpClient.Available > 0)
                    _udpClient.Client.ReceiveFrom(_receiveBuffer, ref _reusableEndpoint);
            }
            catch { }
        }
    }

    public void Shutdown() { _udpClient?.Close(); IsActive = false; }
}
