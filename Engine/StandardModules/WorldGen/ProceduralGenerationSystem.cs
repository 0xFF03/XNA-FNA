using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Game.Core;
using nkast.Aether.Physics2D.Dynamics;

using FlecsWorld = Flecs.NET.Core.World;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Engine.StandardModules.WorldGen;

public static class ProceduralGenerationSystem
{
    public static void Register(FlecsWorld world)
    {
        world.Observer<PhysicsDimension>("ProceduralDimensionObserver")
            .Event(Ecs.OnSet)
            .Each((Entity e, ref PhysicsDimension dim) =>
            {
                if (dim.Name.StartsWith("ProceduralSector_"))
                {
                    GenerateSector(world, dim.Name);
                }
            });
    }

    private static void GenerateSector(FlecsWorld world, string dimensionName)
    {
        var physicsWorld = PhysicsWorldManager.GetWorld(dimensionName);
        Random rand = new Random(dimensionName.GetHashCode());

        for (int i = 0; i < 25; i++)
        {
            float startX = rand.Next(-5000, 5000);
            float startY = rand.Next(-5000, 5000);
            float radius = rand.Next(32, 128);

            var initialPos = new AetherVector2(startX / PhysicsSettings.PixelsPerMeter, startY / PhysicsSettings.PixelsPerMeter);
            var aetherBody = physicsWorld.CreateCircle(radius / PhysicsSettings.PixelsPerMeter, 10f, initialPos, BodyType.Static);

            aetherBody.FixedRotation = false;

            if (aetherBody.FixtureList.Count > 0)
            {
                aetherBody.FixtureList[0].CollisionCategories = PhysicsLayers.Environment;
                aetherBody.FixtureList[0].CollidesWith = PhysicsLayers.LocalPlayer | PhysicsLayers.RemotePlayer | PhysicsLayers.EnemyAndProjectiles;
            }

            ulong netId = NetworkIdGenerator.GetNextNetworkId();

            Entity asteroid = world.Entity($"asteroid_{netId}")
                .Set(new Position { X = startX, Y = startY, Rotation = 0f })
                .Set(new PhysicsDimension { Name = dimensionName })
                .Set(new PhysicsComponents.PhysicsBody { Value = aetherBody })
                .Set(new NetworkId { Value = netId });

            NetworkRegistry.Add(netId, asteroid);
        }
    }
}
