using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Flecs.NET.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Rendering2D;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Game.Core;

using FlecsEntity = Flecs.NET.Core.Entity;

namespace MyGame.Game.Rendering;

public enum CameraViewMode
{
    InteriorCrew,
    ExteriorPiloting
}

public class GameplayRenderer
{
    public Camera2D Camera { get; private set; }
    public Camera2D ExteriorCamera { get; private set; }

    public GameplayRenderer()
    {
        Camera = new Camera2D { Zoom = 1.5f };
        ExteriorCamera = new Camera2D { Zoom = 1.5f };
    }

    public void DrawScene(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D renderTarget, float alpha,
                          CameraViewMode viewMode, float altitudeRatio, Vector2 activeCameraTarget, string interiorDimension, string exteriorDimension,
                          ulong activeInteriorVehicleId, int virtualWidth, int virtualHeight)
    {
        graphicsDevice.SetRenderTarget(renderTarget);
        graphicsDevice.Clear(Color.FromNonPremultiplied(15, 10, 20, 255));

        if (viewMode == CameraViewMode.ExteriorPiloting)
        {
            ExteriorCamera.Position = activeCameraTarget;
            ExteriorCamera.Rotation = 0f;

            ExteriorCamera.Zoom = MathHelper.Lerp(1.5f, 0.6f, altitudeRatio);
            float parallaxScale = MathHelper.Lerp(1.0f, 0.3f, altitudeRatio);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, ExteriorCamera.GetParallaxViewMatrix(virtualWidth, virtualHeight, parallaxScale));
            var macroMap = LevelManager.GetCachedLevel(exteriorDimension);
            if (macroMap != null) TileMapRenderer.Draw(macroMap, ExteriorCamera, virtualWidth, virtualHeight, parallaxScale);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, ExteriorCamera.GetViewMatrix(virtualWidth, virtualHeight));
            PlaceholderVehicleRenderer.Draw(spriteBatch, alpha, exteriorDimension, 0);
            PlaceholderPlayerRenderer.Draw(spriteBatch, alpha, exteriorDimension);
            PlaceholderProjectileRenderer.Draw(spriteBatch, alpha, exteriorDimension);
            spriteBatch.End();
        }
        else // CameraViewMode.InteriorCrew
        {
            Camera.Position = activeCameraTarget;

            if (activeInteriorVehicleId != 0)
            {
                FlecsEntity linkedExteriorVehicle = NetworkRegistry.GetEntity(activeInteriorVehicleId);

                if (linkedExteriorVehicle.IsAlive() && linkedExteriorVehicle.Has<Position>() && linkedExteriorVehicle.Has<PhysicsDimension>())
                {
                    var vPos = linkedExteriorVehicle.Get<Position>();
                    var vPrev = linkedExteriorVehicle.Has<PreviousPosition>() ? linkedExteriorVehicle.Get<PreviousPosition>() : new PreviousPosition { X = vPos.X, Y = vPos.Y, Rotation = vPos.Rotation };

                    float vX = MathHelper.Lerp(vPrev.X, vPos.X, alpha);
                    float vY = MathHelper.Lerp(vPrev.Y, vPos.Y, alpha);
                    Vector2 exteriorShipPos = new Vector2(vX, vY);
                    float exteriorShipRot = MathHelper.WrapAngle(MathHelper.Lerp(vPrev.Rotation, vPos.Rotation, alpha));

                    var interiorMap = LevelManager.GetCachedLevel(interiorDimension);
                    Vector2 interiorCenter = interiorMap?.Center ?? Vector2.Zero;

                    Vector2 playerLocalOffset = activeCameraTarget - interiorCenter;
                    Vector2 rotatedOffset = Vector2.Transform(playerLocalOffset, Matrix.CreateRotationZ(exteriorShipRot));

                    ExteriorCamera.Position = exteriorShipPos + rotatedOffset;
                    ExteriorCamera.Rotation = -exteriorShipRot;

                    float currentShipAltitude = linkedExteriorVehicle.Has<VehicleFlightState>() ? linkedExteriorVehicle.Get<VehicleFlightState>().AltitudeRatio : 0f;
                    ExteriorCamera.Zoom = MathHelper.Lerp(1.5f, 0.6f, currentShipAltitude);
                    float parallaxScale = MathHelper.Lerp(1.0f, 0.3f, currentShipAltitude);

                    // ARCHITECTURE FIX: SamplerState.LinearClamp eliminates the sharp, pixelated stair-step tearing that occurs when low-res maps are forcefully rotated.
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, null, null, null, ExteriorCamera.GetParallaxViewMatrix(virtualWidth, virtualHeight, parallaxScale));
                    var exteriorMap = LevelManager.GetCachedLevel(exteriorDimension);
                    if (exteriorMap != null) TileMapRenderer.Draw(exteriorMap, ExteriorCamera, virtualWidth, virtualHeight, parallaxScale);
                    spriteBatch.End();

                    // Uses LinearClamp so the exterior ship hulls also rotate smoothly outside the window
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, null, null, null, ExteriorCamera.GetViewMatrix(virtualWidth, virtualHeight));
                    PlaceholderVehicleRenderer.Draw(spriteBatch, alpha, exteriorDimension, activeInteriorVehicleId);
                    PlaceholderPlayerRenderer.Draw(spriteBatch, alpha, exteriorDimension);
                    PlaceholderProjectileRenderer.Draw(spriteBatch, alpha, exteriorDimension);
                    spriteBatch.End();
                }
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, Camera.GetViewMatrix(virtualWidth, virtualHeight));

            var currentMap = LevelManager.GetCachedLevel(interiorDimension);
            if (currentMap != null) TileMapRenderer.Draw(currentMap, Camera, virtualWidth, virtualHeight);

            PlaceholderVehicleRenderer.Draw(spriteBatch, alpha, interiorDimension, 0);
            PlaceholderPlayerRenderer.Draw(spriteBatch, alpha, interiorDimension);
            PlaceholderProjectileRenderer.Draw(spriteBatch, alpha, interiorDimension);

            spriteBatch.End();
        }
    }
}
