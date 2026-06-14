using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework.Input;
using MyGame.Gameplay.Components;

namespace MyGame.Gameplay.Systems;

public static class LocalPlayerSystems
{
    private const float MoveSpeed = 200f;

    public static void Register(World world)
    {
        world.System<LocalInput>("InputGatheringSystem")
            .With<LocalPlayerTag>()
            .Each((ref LocalInput input) =>
            {
                input.AxisX = 0;
                input.AxisY = 0;

                // If the player alt-tabs or opens the Steam Overlay, instantly zero out movement.
                if (!Game1.Instance.IsActive) return;

                var kstate = Keyboard.GetState();

                if (kstate.IsKeyDown(Keys.W) || kstate.IsKeyDown(Keys.Up)) input.AxisY -= 1;
                if (kstate.IsKeyDown(Keys.S) || kstate.IsKeyDown(Keys.Down)) input.AxisY += 1;
                if (kstate.IsKeyDown(Keys.A) || kstate.IsKeyDown(Keys.Left)) input.AxisX -= 1;
                if (kstate.IsKeyDown(Keys.D) || kstate.IsKeyDown(Keys.Right)) input.AxisX += 1;

                if (input.AxisX != 0 || input.AxisY != 0)
                {
                    float length = (float)Math.Sqrt((input.AxisX * input.AxisX) + (input.AxisY * input.AxisY));
                    input.AxisX /= length;
                    input.AxisY /= length;
                }
            });

        world.System<Velocity, LocalInput>("ApplyInputSystem")
            .With<LocalPlayerTag>()
            .Each((ref Velocity vel, ref LocalInput input) =>
            {
                vel.X = input.AxisX * MoveSpeed;
                vel.Y = input.AxisY * MoveSpeed;
            });

        world.System<Position, Velocity>("ApplyLocalMovementSystem")
            .With<LocalPlayerTag>()
            .Each((Iter it, int _, ref Position pos, ref Velocity vel) =>
            {
                float dt = it.DeltaTime();
                pos.X += vel.X * dt;
                pos.Y += vel.Y * dt;
            });
    }
}
