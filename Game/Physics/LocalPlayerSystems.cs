using System;
using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MyGame.Engine.Input;
using MyGame.Prefabs;

using MyGame.Game.Core;
using MyGame.Game.Physics;

namespace MyGame.Game.Physics;

public static class LocalPlayerSystems
{
    public static void Register(World world)
    {
        world.System<LocalInput, FacingDirection>("InputGatheringSystem")
            .Kind(Ecs.PreUpdate)
            .With<LocalPlayerTag>()
            .Each((ref LocalInput input, ref FacingDirection facing) =>
            {
                if (!Game1.Instance.IsActive) return;

                float dx = 0;
                if (InputManager.IsActionActive(GameActions.MoveLeft))  dx -= 1;
                if (InputManager.IsActionActive(GameActions.MoveRight)) dx += 1;

                input.AxisX = dx;

                if (dx != 0) facing.Value = dx > 0 ? 1 : -1;

                if (InputManager.ConsumeAction(GameActions.Jump))
                {
                    input.JumpJustPressed = true;
                }
            });

        world.System<PhysicsBody, GroundState>("GroundDetectionSystem")
            .Kind(Ecs.PreUpdate)
            .With<LocalPlayerTag>()
            .Each((ref PhysicsBody pBody, ref GroundState ground) =>
            {
                if (pBody.Value == null) return;
                var body = (nkast.Aether.Physics2D.Dynamics.Body)pBody.Value;

                bool touchingSomething = body.ContactList != null;
                ground.IsGrounded = touchingSomething;

                if (ground.IsGrounded)
                {
                    ground.CoyoteTimer = 0.1f;
                }
                else if (ground.CoyoteTimer > 0f)
                {
                    ground.CoyoteTimer -= 1f / 60f;
                }
            });

        world.System<PhysicsBody, LocalInput, GroundState>("LocalMovementSystem")
            .Kind(Ecs.OnUpdate)
            .With<LocalPlayerTag>()
            .Each((ref PhysicsBody pBody, ref LocalInput input, ref GroundState ground) =>
            {
                if (pBody.Value == null) return;
                var body = (nkast.Aether.Physics2D.Dynamics.Body)pBody.Value;
                var currentVel = body.LinearVelocity;

                float targetXVel = input.AxisX * 8f;
                float velPower = 0.2f;
                float newXVel = MathHelper.Lerp(currentVel.X, targetXVel, velPower);

                float newYVel = currentVel.Y;
                if (input.JumpJustPressed && (ground.IsGrounded || ground.CoyoteTimer > 0f))
                {
                    newYVel = -12f;
                    ground.CoyoteTimer = 0f;
                }
                input.JumpJustPressed = false;

                body.LinearVelocity = new nkast.Aether.Physics2D.Common.Vector2(newXVel, newYVel);
            });

        world.System<PhysicsBody, Position, Velocity>("SyncLocalPhysicsToEcsSystem")
            .Kind(Ecs.PostUpdate)
            .With<LocalPlayerTag>()
            .Each((ref PhysicsBody pBody, ref Position pos, ref Velocity velocity) =>
            {
                if (pBody.Value == null) return;
                var body = (nkast.Aether.Physics2D.Dynamics.Body)pBody.Value;

                pos.X = body.Position.X * PlayerFactory.PixelsPerMeter;
                pos.Y = body.Position.Y * PlayerFactory.PixelsPerMeter;

                velocity.X = body.LinearVelocity.X;
                velocity.Y = body.LinearVelocity.Y;
            });
    }
}
