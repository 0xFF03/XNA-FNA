using System;
using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class KinematicInterpolator
{
    public static void Register(Flecs.NET.Core.World world)
    {
        world.System<Position, TargetPosition, Velocity, NetworkSequence>("RemoteInterpolationSystem")
            .Kind(Ecs.OnUpdate)
            .With<RemotePlayerTag>()
            .Without<LocalPlayerTag>()
            .Each((Iter it, int i, ref Position pos, ref TargetPosition target, ref Velocity velocity, ref NetworkSequence seq) =>
            {
                float distanceX = target.X - pos.X;
                float distanceY = target.Y - pos.Y;

                if (MathF.Abs(distanceX) > 64f || MathF.Abs(distanceY) > 64f)
                {
                    pos.X = target.X;
                    pos.Y = target.Y;
                    pos.Rotation = target.Rotation;
                }
                else
                {
                    float smoothingRate = 1f - MathF.Exp(-12f * it.DeltaTime());
                    pos.X = MathHelper.Lerp(pos.X, target.X, smoothingRate);
                    pos.Y = MathHelper.Lerp(pos.Y, target.Y, smoothingRate);

                    // Smooth path angle wrapping
                    pos.Rotation = MathHelper.WrapAngle(MathHelper.Lerp(pos.Rotation, target.Rotation, smoothingRate));
                }

                Entity e = it.Entity(i);
                if (e.Has<PhysicsComponents.PhysicsBody>())
                {
                    var pBody = e.Get<PhysicsComponents.PhysicsBody>();
                    if (pBody.Value != null)
                    {
                        var body = pBody.Value;
                        body.Position = new nkast.Aether.Physics2D.Common.Vector2(
                            pos.X / PhysicsSettings.PixelsPerMeter,
                            pos.Y / PhysicsSettings.PixelsPerMeter);

                        body.Rotation = pos.Rotation;
                        body.LinearVelocity = nkast.Aether.Physics2D.Common.Vector2.Zero;
                    }
                }
            });
    }
}
