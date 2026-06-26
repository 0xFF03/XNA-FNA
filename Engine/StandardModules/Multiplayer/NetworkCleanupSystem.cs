using Flecs.NET.Core;
using MyGame.Engine.Core;
using MyGame.Engine.Platform.Networking;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Multiplayer;

public static class NetworkCleanupSystem
{
    private static ulong _lastKnownLocalId = 0;

    public static void Register(World world)
    {
        // ARCHITECTURE FIX: Seamlessly migrates ECS ownership when hot-swapping Network Providers mid-game.
        world.System<NetworkOwner>("NetworkIdentityMigrationSystem")
            .Kind(Ecs.PreUpdate)
            .Each((Entity e, ref NetworkOwner owner) =>
            {
                var net = NetworkServiceLocator.Provider;
                if (net.IsActive && _lastKnownLocalId != 0 && _lastKnownLocalId != net.LocalUserId)
                {
                    if (owner.Value == _lastKnownLocalId)
                    {
                        owner.Value = net.LocalUserId;
                        EngineLogger.Log($"Migrated ECS ownership of Entity {e.Id} to new Network Identity: {net.LocalUserId}", "NETWORK");
                    }
                }
            });

        world.System("UpdateLastKnownIdSystem")
            .Kind(Ecs.PostUpdate)
            .Iter((Iter it) =>
            {
                var net = NetworkServiceLocator.Provider;
                if (net.IsActive) _lastKnownLocalId = net.LocalUserId;
            });

        world.System<NetworkOwner, NetworkId>("NetworkDisconnectSweepSystem")
            .Kind(Ecs.PreUpdate)
            .Interval(1.0f)
            .Each((Iter it, int row, ref NetworkOwner owner, ref NetworkId netId) =>
            {
                var net = NetworkServiceLocator.Provider;

                if (!net.IsActive || !net.IsInLobby) return;

                if (owner.Value == net.LocalUserId) return;

                Entity e = it.Entity(row);

                // ARCHITECTURE FIX: Hard-lock protection. The sweeper is legally forbidden from deleting the local screen avatar or structural environment entities.
                if (e.Has<LocalPlayerTag>() || e.Has<InteractableTag>()) return;

                if (!net.ActivePeers.Contains(owner.Value))
                {
                    EngineLogger.Log($"Sweeping orphaned entity {netId.Value} from disconnected peer {owner.Value}.", "NETWORK");
                    e.Destruct();
                }
            });
    }
}
