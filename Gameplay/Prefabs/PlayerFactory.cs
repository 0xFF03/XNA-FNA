using Flecs.NET.Core;
using Steamworks;
using MyGame.Gameplay.Components;

namespace MyGame.Gameplay.Prefabs;

public static class PlayerFactory
{
	public static Entity CreateLocal(World world, int classId)
	{
		return world.Entity("LocalPlayer")
			.Add<LocalPlayerTag>()
			.Set(new Position { X = 400, Y = 300 })
			.Set(new Velocity { X = 0, Y = 0 })
			.Set(new CharacterClass { Id = classId })
			.Set(new NetworkOwner { Value = SteamClient.SteamId });
	}

	public static Entity CreateRemote(World world, string entityKey, PlayerTransformPacket packet, SteamId senderId)
	{
		return world.Entity(entityKey)
			.Add<RemotePlayerTag>()
			.Set(new Position { X = packet.X, Y = packet.Y })
			.Set(new Velocity { X = packet.Vx, Y = packet.Vy })
			.Set(new CharacterClass { Id = packet.CharacterClassId })
			.Set(new NetworkOwner { Value = senderId });
	}
}
