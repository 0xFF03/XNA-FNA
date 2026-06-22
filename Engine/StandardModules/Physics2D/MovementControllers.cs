using System;
using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MyGame.Engine.Platform;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class MovementControllers
{
    // ARCHITECTURE FIX: Securely fully qualified to eliminate compiler ambiguity
    public static void Register(Flecs.NET.Core.World world)
    {
        world.System<LocalInput, FacingDirection>("InputGatheringSystem")
            .Kind(Ecs.PreUpdate)
            .With<LocalPlayerTag>()
            .Each((Entity e, ref LocalInput input, ref FacingDirection facing) =>
            {
                float dx = 0;
                if (InputManager.IsActionActive(GameActions.MoveLeft))  dx -= 1;
                if (InputManager.IsActionActive(GameActions.MoveRight)) dx += 1;

                float dy = 0;
                if (InputManager.IsActionActive(GameActions.MoveUp))    dy -= 1;
                if (InputManager.IsActionActive(GameActions.MoveDown))  dy += 1;

                if (e.Has<HelmControl>())
                {
                    var helm = e.Get<HelmControl>();

                    if (helm.ControlledVehicle.Id != 0 && helm.ControlledVehicle.IsAlive())
                    {
                        helm.ControlledVehicle.Set(new LocalInput { AxisX = dx, AxisY = dy, JumpJustPressed = InputManager.ConsumeAction(GameActions.Jump) });

                        input.AxisX = 0;
                        input.AxisY = 0;
                        return;
                    }
                }

                input.AxisX = dx;
                input.AxisY = dy;

                if (dx != 0) facing.Value = dx > 0 ? 1 : -1;

                if (InputManager.ConsumeAction(GameActions.Jump))
                {
                    input.JumpJustPressed = true;
                }
            });

        world.System<PhysicsComponents.PhysicsBody, GroundState>("SidescrollerGroundDetection")
            .Kind(Ecs.PreUpdate)
            .Without<RemotePlayerTag>()
            .With<SidescrollerTag>()
            .Each((ref PhysicsComponents.PhysicsBody pBody, ref GroundState ground) =>
            {
                if (pBody.Value == null) return;
                var body = pBody.Value;

                bool touchingSomething = body.ContactList != null;
                ground.IsGrounded = touchingSomething;

                if (ground.IsGrounded) ground.CoyoteTimer = 0.1f;
                else if (ground.CoyoteTimer > 0f) ground.CoyoteTimer -= 1f / 60f;
            });

        world.System<PhysicsComponents.PhysicsBody, LocalInput, GroundState, MovementCapabilities>("SidescrollerMovementSystem")
            .Kind(Ecs.OnUpdate)
            .Without<RemotePlayerTag>()
            .With<SidescrollerTag>()
            .Each((ref PhysicsComponents.PhysicsBody pBody, ref LocalInput input, ref GroundState ground, ref MovementCapabilities caps) =>
            {
                if (pBody.Value == null) return;
                var body = pBody.Value;
                var currentVel = body.LinearVelocity;

                float targetXVel = input.AxisX * caps.MoveSpeed;
                float newXVel = MathHelper.Lerp(currentVel.X, targetXVel, 0.2f);
                float newYVel = currentVel.Y;

                if (input.JumpJustPressed && (ground.IsGrounded || ground.CoyoteTimer > 0f))
                {
                    newYVel = caps.JumpForce;
                    ground.CoyoteTimer = 0f;
                }
                input.JumpJustPressed = false;

                body.LinearVelocity = new nkast.Aether.Physics2D.Common.Vector2(newXVel, newYVel);
            });

        world.System<PhysicsComponents.PhysicsBody, LocalInput, MovementCapabilities>("TopDownMovementSystem")
            .Kind(Ecs.OnUpdate)
            .Without<RemotePlayerTag>()
            .With<TopDownTag>()
            .Each((ref PhysicsComponents.PhysicsBody pBody, ref LocalInput input, ref MovementCapabilities caps) =>
            {
                if (pBody.Value == null) return;
                var body = pBody.Value;
                var currentVel = body.LinearVelocity;

                float targetXVel = input.AxisX * caps.MoveSpeed;
                float targetYVel = input.AxisY * caps.MoveSpeed;

                if (input.AxisX != 0 && input.AxisY != 0)
                {
                    targetXVel *= 0.7071f;
                    targetYVel *= 0.7071f;
                }

                float newXVel = MathHelper.Lerp(currentVel.X, targetXVel, 0.2f);
                float newYVel = MathHelper.Lerp(currentVel.Y, targetYVel, 0.2f);

                body.LinearVelocity = new nkast.Aether.Physics2D.Common.Vector2(newXVel, newYVel);
            });

        world.System<PhysicsComponents.PhysicsBody, Position, Velocity>("SyncLocalPhysicsToEcsSystem")
            .Kind(Ecs.PostUpdate)
            .Without<RemotePlayerTag>()
            .Each((ref PhysicsComponents.PhysicsBody pBody, ref Position pos, ref Velocity velocity) =>
            {
                if (pBody.Value == null) return;
                var body = pBody.Value;

                pos.X = body.Position.X * PhysicsSettings.PixelsPerMeter;
                pos.Y = body.Position.Y * PhysicsSettings.PixelsPerMeter;

                velocity.X = body.LinearVelocity.X;
                velocity.Y = body.LinearVelocity.Y;
            });
    }
}
