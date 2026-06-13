using Flecs.NET.Core;
using Steamworks;
using MyGame.Engine.Networking;
using MyGame.Gameplay.Components;
using MyGame.Gameplay;

namespace MyGame.Gameplay.Systems;

public static class NetworkBroadcastSystem
{
	// Pre-allocated pointer buffer shared by the system
	private static readonly byte[] _transformBuffer = new byte[System.Runtime.InteropServices.Marshal.SizeOf<PlayerTransformPacket>()];

	public static void Register(World world)
	{
		world.System<Position, Velocity, CharacterClass>("NetworkBroadcastSystem")
			.Kind(Ecs.PostUpdate) // Guarantees this runs AFTER movement physics are calculated
			.With<LocalPlayerTag>()
			.Interval(1f / 30f)   // Flecs native timer: Throttles execution to 30 times a second automatically!
			.Each((Entity _, ref Position pos, ref Velocity vel, ref CharacterClass cClass) =>
			{
				if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue) return;

				var packet = new PlayerTransformPacket
				{
					PacketType = 1,
					CharacterClassId = cClass.Id,
					X = pos.X,
					Y = pos.Y,
					Vx = vel.X,
					Vy = vel.Y
				};

				packet.SerializeTo(_transformBuffer);

				foreach (var member in SteamManager.CurrentLobby.Value.Members)
				{
					if (member.Id == SteamClient.SteamId) continue;
					SteamNetworking.SendP2PPacket(member.Id, _transformBuffer, _transformBuffer.Length, 0, P2PSend.Unreliable);
				}
			});
	}
}
