using System;
using Microsoft.Xna.Framework;
using Flecs.NET.Core;
using MyGame.Engine.Platform;
using MyGame.Game.Core;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Engine.StandardModules.Physics2D;

public static class MovementControllers
{
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
                        helm.ControlledVehicle.Set(new LocalInput {
                            AxisX = dx,
                            AxisY = dy,
                            JumpJustPressed = InputManager.ConsumeAction(GameActions.Jump),
                            FlightJustPressed = InputManager.ConsumeAction(GameActions.FlightToggle)
                        });

                        input.AxisX = 0;
                        input.AxisY = 0;
                        return;
                    }
                }

                input.AxisX = dx;
                input.AxisY = dy;

                if (dx != 0) facing.Value = dx > 0 ? 1 : -1;

                input.JumpJustPressed = InputManager.ConsumeAction(GameActions.Jump);
                input.FlightJustPressed = InputManager.ConsumeAction(GameActions.FlightToggle);
            });

        world.System<PhysicsComponents.PhysicsBody, GroundState>("SidescrollerGroundDetection")
            .Kind(Ecs.PreUpdate)
            .Without<RemotePlayerTag>()
            .With<SidescrollerTag>()
            .Each((Iter it, int i, ref PhysicsComponents.PhysicsBody pBody, ref GroundState ground) =>
            {
                if (pBody.Value == null) return;
                var body = pBody.Value;

                bool touchingSomething = body.ContactList != null;
                ground.IsGrounded = touchingSomething;

                if (ground.IsGrounded) ground.CoyoteTimer = 0.1f;
                else if (ground.CoyoteTimer > 0f) ground.CoyoteTimer -= it.DeltaTime();
            });

        world.System<PhysicsComponents.PhysicsBody, LocalInput, GroundState, MovementCapabilities>("SidescrollerMovementSystem")
            .Kind(Ecs.OnUpdate)
            .Without<RemotePlayerTag>()
            .With<SidescrollerTag>()
            .Each((Iter it, int i, ref PhysicsComponents.PhysicsBody pBody, ref LocalInput input, ref GroundState ground, ref MovementCapabilities caps) =>
            {
                if (pBody.Value == null) return;
                var body = pBody.Value;
                var currentVel = body.LinearVelocity;

                float targetXVel = input.AxisX * caps.MoveSpeed;
                float smoothingRate = 1f - MathF.Exp(-15f * it.DeltaTime());
                float newXVel = MathHelper.Lerp(currentVel.X, targetXVel, smoothingRate);
                float newYVel = currentVel.Y;

                if (input.JumpJustPressed && (ground.IsGrounded || ground.CoyoteTimer > 0f))
                {
                    newYVel = caps.JumpForce;
                    ground.CoyoteTimer = 0f;
                }

                if (input.AxisX != 0 || input.JumpJustPressed) body.Awake = true;

                body.LinearVelocity = new AetherVector2(newXVel, newYVel);
            });

        world.System<PhysicsComponents.PhysicsBody, LocalInput, MovementCapabilities, Position>("TopDownMovementSystem")
            .Kind(Ecs.OnUpdate)
            .Without<RemotePlayerTag>()
            .With<TopDownTag>()
            .Each((Iter it, int i, ref PhysicsComponents.PhysicsBody pBody, ref LocalInput input, ref MovementCapabilities caps, ref Position pos) =>
            {
                Entity e = it.Entity(i);
                if (pBody.Value == null) return;
                var body = pBody.Value;

                if (input.AxisX != 0 || input.AxisY != 0) body.Awake = true;

                if (e.Has<RotationalDriveTag>() && e.Has<ShipEngine>())
                {
                    ref var engine = ref e.GetMut<ShipEngine>();
                    bool canMove = true;

                    // ARCHITECTURE FIX: Processes Flight Toggle and Lerps altitude organically over time
                    if (e.Has<VehicleFlightState>())
                    {
                        ref var flight = ref e.GetMut<VehicleFlightState>();

                        if (input.FlightJustPressed)
                        {
                            flight.TargetFlying = !flight.TargetFlying;
                            Core.EngineLogger.Log($"Flight Systems: {(flight.TargetFlying ? "ENGAGED" : "LANDING")}", "SYSTEM");
                        }

                        float targetRatio = flight.TargetFlying ? 1.0f : 0.0f;
                        flight.AltitudeRatio = MathHelper.Lerp(flight.AltitudeRatio, targetRatio, 1f - MathF.Exp(-2f * it.DeltaTime()));

                        // Ground interlock: Engines are mathematically disabled if we are currently parked or landing
                        if (flight.AltitudeRatio < 0.9f) canMove = false;
                    }

                    float thrustInput = canMove ? -input.AxisY : 0f;

                    engine.CurrentThrust = MathHelper.Lerp(engine.CurrentThrust, thrustInput, 1f - MathF.Exp(-2f * it.DeltaTime()));

                    if (MathF.Abs(engine.CurrentThrust) > 0.01f)
                    {
                        float rot = body.Rotation;
                        float forwardX = MathF.Sin(rot);
                        float forwardY = -MathF.Cos(rot);

                        AetherVector2 worldForward = new AetherVector2(forwardX, forwardY);

                        // ARCHITECTURE FIX: Reduced thrust power heavily. Ship preserves mass but accelerates cleanly.
                        float thrustPower = caps.MoveSpeed * body.Mass * 4f;

                        body.ApplyForce(worldForward * (engine.CurrentThrust * thrustPower));
                    }

                    if (canMove && input.AxisX != 0)
                    {
                        float targetAngularVel = input.AxisX * 3.5f;
                        body.AngularVelocity = MathHelper.Lerp(body.AngularVelocity, targetAngularVel, 1f - MathF.Exp(-6f * it.DeltaTime()));
                    }
                    else if (!canMove)
                    {
                        // Artificial ground friction to force the ship to mathematically stop moving when parked
                        body.LinearVelocity = new AetherVector2(
                            MathHelper.Lerp(body.LinearVelocity.X, 0, 1f - MathF.Exp(-5f * it.DeltaTime())),
                            MathHelper.Lerp(body.LinearVelocity.Y, 0, 1f - MathF.Exp(-5f * it.DeltaTime()))
                        );
                        body.AngularVelocity = MathHelper.Lerp(body.AngularVelocity, 0, 1f - MathF.Exp(-5f * it.DeltaTime()));
                    }
                }
                else
                {
                    var currentVel = body.LinearVelocity;
                    float targetXVel = input.AxisX * caps.MoveSpeed;
                    float targetYVel = input.AxisY * caps.MoveSpeed;

                    if (input.AxisX != 0 && input.AxisY != 0)
                    {
                        targetXVel *= 0.7071f;
                        targetYVel *= 0.7071f;
                    }

                    float inertia = 1f - MathF.Exp(-15f * it.DeltaTime());

                    float newXVel = MathHelper.Lerp(currentVel.X, targetXVel, inertia);
                    float newYVel = MathHelper.Lerp(currentVel.Y, targetYVel, inertia);

                    body.LinearVelocity = new AetherVector2(newXVel, newYVel);

                    if (input.WorldMousePosition != Vector2.Zero && e.Has<LocalPlayerTag>())
                    {
                        Vector2 playerScreenPos = new Vector2(pos.X, pos.Y);
                        Vector2 direction = input.WorldMousePosition - playerScreenPos;

                        if (direction != Vector2.Zero)
                        {
                            float targetRotation = MathF.Atan2(direction.Y, direction.X) + MathHelper.PiOver2;
                            body.Rotation = MathHelper.WrapAngle(MathHelper.Lerp(body.Rotation, targetRotation, 1f - MathF.Exp(-20f * it.DeltaTime())));
                        }
                    }
                }
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
                pos.Rotation = body.Rotation;

                velocity.X = body.LinearVelocity.X;
                velocity.Y = body.LinearVelocity.Y;
            });
    }
}
