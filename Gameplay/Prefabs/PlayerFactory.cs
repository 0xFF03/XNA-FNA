using Flecs.NET.Core;
using Steamworks;
using MyGame.Engine.Networking; // Imported for the ID Generator
using MyGame.Gameplay.Components;
using MyGame.Gameplay.Networking;

namespace MyGame.Gameplay.Prefabs;

public static class PlayerFactory
{
	public static Entity CreateLocal(World world, int classId)
	{
		return world.Entity("LocalPlayer")
			.Add<LocalPlayerTag>()
			.Add<MatchEntityTag>()
			.Set(new LocalInput { AxisX = 0, AxisY = 0 })
			.Set(new Position { X = 400, Y = 300 })
			.Set(new Velocity { X = 0, Y = 0 })
			.Set(new CharacterClass { Id = classId })
			.Set(new NetworkOwner { Value = SteamClient.SteamId })
			.Set(new NetworkId { Value = NetworkIdGenerator.GetNextUniqueId() });
	}

	public static Entity CreateRemote(World world, string entityKey, PlayerTransformPacket packet, SteamId senderId)
	{
		return world.Entity(entityKey)
			.Add<RemotePlayerTag>()
			.Add<MatchEntityTag>()
			.Set(new Position { X = packet.X, Y = packet.Y })
			.Set(new TargetPosition { X = packet.X, Y = packet.Y })
			.Set(new Velocity { X = packet.Vx, Y = packet.Vy })
			.Set(new CharacterClass { Id = packet.CharacterClassId })
			.Set(new NetworkOwner { Value = senderId })
			.Set(new NetworkId { Value = packet.EntityNetworkSequenceId })
			.Set(new NetworkSequence { LatestSequence = packet.SequenceNumber });
	}
}
