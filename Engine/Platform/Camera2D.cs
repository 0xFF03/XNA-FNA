using System;
using Microsoft.Xna.Framework;

namespace MyGame.Engine.Platform;

public class Camera2D
{
    public Vector2 Position { get; set; }
    public float Zoom { get; set; } = 1f;
    public float Rotation { get; set; } = 0f;
    public Rectangle? Limits { get; set; }

    public Camera2D()
    {
        Position = Vector2.Zero;
    }

    public Vector2 GetClampedPosition(int virtualWidth, int virtualHeight)
    {
        float clampedX = Position.X;
        float clampedY = Position.Y;

        if (Limits.HasValue)
        {
            float halfViewWidth = (virtualWidth * 0.5f) / Zoom;
            float halfViewHeight = (virtualHeight * 0.5f) / Zoom;

            if (Limits.Value.Width < virtualWidth / Zoom) clampedX = Limits.Value.Center.X;
            else clampedX = MathHelper.Clamp(Position.X, Limits.Value.Left + halfViewWidth, Limits.Value.Right - halfViewWidth);

            if (Limits.Value.Height < virtualHeight / Zoom) clampedY = Limits.Value.Center.Y;
            else clampedY = MathHelper.Clamp(Position.Y, Limits.Value.Top + halfViewHeight, Limits.Value.Bottom - halfViewHeight);
        }
        return new Vector2(clampedX, clampedY);
    }

    public Matrix GetViewMatrix(int virtualWidth, int virtualHeight)
    {
        Vector2 clamped = GetClampedPosition(virtualWidth, virtualHeight);

        // ARCHITECTURE FIX: Absolute mathematical rounding prevents the grid from "vibrating" on fractional floats
        float rX = MathF.Round(clamped.X);
        float rY = MathF.Round(clamped.Y);
        float cX = MathF.Round(virtualWidth * 0.5f);
        float cY = MathF.Round(virtualHeight * 0.5f);

        return Matrix.CreateTranslation(new Vector3(-rX, -rY, 0)) *
               Matrix.CreateRotationZ(Rotation) *
               Matrix.CreateScale(new Vector3(Zoom, Zoom, 1)) *
               Matrix.CreateTranslation(new Vector3(cX, cY, 0));
    }

    public Matrix GetParallaxViewMatrix(int virtualWidth, int virtualHeight, float parallaxFactor)
    {
        Vector2 clamped = GetClampedPosition(virtualWidth, virtualHeight) * parallaxFactor;

        float rX = MathF.Round(clamped.X);
        float rY = MathF.Round(clamped.Y);
        float cX = MathF.Round(virtualWidth * 0.5f);
        float cY = MathF.Round(virtualHeight * 0.5f);

        return Matrix.CreateTranslation(new Vector3(-rX, -rY, 0)) *
               Matrix.CreateRotationZ(Rotation) *
               Matrix.CreateScale(new Vector3(Zoom, Zoom, 1)) *
               Matrix.CreateTranslation(new Vector3(cX, cY, 0));
    }
}
