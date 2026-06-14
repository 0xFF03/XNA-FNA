using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using MyGame.Gameplay.Components;

namespace MyGame.Gameplay.Systems;

public static class MovementSystems
{
    private const float LerpSpeedMultiplier = 15f;
    private const float MoveSpeed = 200f;

    public static void Register(World world)
    {
        // 1. INPUT GATHERING: Strictly reads hardware and maps it to an agnostic axis
        world.System<LocalInput>("InputGatheringSystem")
            .With<LocalPlayerTag>()
            .Each((ref LocalInput input) =>
            {
                var kstate = Keyboard.GetState();
                input.AxisX = 0;
                input.AxisY = 0;

                if (kstate.IsKeyDown(Keys.W) || kstate.IsKeyDown(Keys.Up)) input.AxisY = -1;
                if (kstate.IsKeyDown(Keys.S) || kstate.IsKeyDown(Keys.Down)) input.AxisY = 1;
                if (kstate.IsKeyDown(Keys.A) || kstate.IsKeyDown(Keys.Left)) input.AxisX = -1;
                if (kstate.IsKeyDown(Keys.D) || kstate.IsKeyDown(Keys.Right)) input.AxisX = 1;
            });

        // 2. INPUT APPLICATION: Translates the agnostic axis into physical velocity
        world.System<Velocity, LocalInput>("ApplyInputSystem")
            .With<LocalPlayerTag>()
            .Each((ref Velocity vel, ref LocalInput input) =>
            {
                vel.X = input.AxisX * MoveSpeed;
                vel.Y = input.AxisY * MoveSpeed;
            });

        // 3. LOCAL MOVEMENT: Standard integration
        world.System<Position, Velocity>("ApplyLocalMovementSystem")
            .With<LocalPlayerTag>()
            .Each((Iter it, int _, ref Position pos, ref Velocity vel) =>
            {
                float dt = it.DeltaTime();
                pos.X += vel.X * dt;
                pos.Y += vel.Y * dt;
            });

        // 4. REMOTE LERP: Safe, clamped interpolation
        world.System<Position, TargetPosition, Velocity>("ApplyRemoteLerpSystem")
            .With<RemotePlayerTag>()
            .Each((Iter it, int _, ref Position pos, ref TargetPosition target, ref Velocity vel) =>
            {
                float dt = it.DeltaTime();
                target.X += vel.X * dt;
                target.Y += vel.Y * dt;

                float t = System.Math.Min(LerpSpeedMultiplier * dt, 1f);
                pos.X += (target.X - pos.X) * t;
                pos.Y += (target.Y - pos.Y) * t;
            });
    }
}
