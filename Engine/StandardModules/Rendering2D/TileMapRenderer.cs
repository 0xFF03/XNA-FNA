using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Platform;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.Engine.StandardModules.Rendering2D;

public static class TileMapRenderer
{
    private static SpriteBatch _spriteBatch = null!;

    public static void Initialize(Flecs.NET.Core.World world, SpriteBatch batch)
    {
        _spriteBatch = batch;
    }

    private static int MathWrap(int value, int max)
    {
        int r = value % max;
        return r < 0 ? r + max : r;
    }

    public static void Draw(LevelData roomData, Camera2D camera, int virtualWidth, int virtualHeight, float parallaxFactor = 1.0f)
    {
        if (roomData == null) return;

        Vector2 clampedCamPos = camera.GetClampedPosition(virtualWidth, virtualHeight) * parallaxFactor;
        float zoom = camera.Zoom <= 0f ? 1f : camera.Zoom;

        // ARCHITECTURE FIX: Huge chunk padding ensures no black void rectangles ever appear when flying in deep space
        float viewPadding = LevelData.ChunkSize * 2f;
        float viewLeft = clampedCamPos.X - (virtualWidth / 2f / zoom) - viewPadding;
        float viewRight = clampedCamPos.X + (virtualWidth / 2f / zoom) + viewPadding;
        float viewTop = clampedCamPos.Y - (virtualHeight / 2f / zoom) - viewPadding;
        float viewBottom = clampedCamPos.Y + (virtualHeight / 2f / zoom) + viewPadding;

        int minChunkX = (int)(viewLeft / LevelData.ChunkSize);
        int maxChunkX = (int)(viewRight / LevelData.ChunkSize);
        int minChunkY = (int)(viewTop / LevelData.ChunkSize);
        int maxChunkY = (int)(viewBottom / LevelData.ChunkSize);

        int mapChunksX = (int)System.Math.Ceiling(roomData.Width / (float)LevelData.ChunkSize);
        int mapChunksY = (int)System.Math.Ceiling(roomData.Height / (float)LevelData.ChunkSize);
        if (mapChunksX <= 0) mapChunksX = 1;
        if (mapChunksY <= 0) mapChunksY = 1;

        for (int cy = minChunkY; cy <= maxChunkY; cy++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                // ARCHITECTURE FIX: Mathematically wraps coordinates creating a literally infinite seamless background
                int lookupCx = roomData.IsInfinite ? MathWrap(cx, mapChunksX) : cx;
                int lookupCy = roomData.IsInfinite ? MathWrap(cy, mapChunksY) : cy;

                Point targetChunk = new Point(lookupCx, lookupCy);
                if (roomData.TileChunks.TryGetValue(targetChunk, out var chunkTiles))
                {
                    float offsetX = (cx - lookupCx) * LevelData.ChunkSize;
                    float offsetY = (cy - lookupCy) * LevelData.ChunkSize;

                    foreach (var tile in chunkTiles)
                    {
                        if (tile.Texture != null && !tile.Texture.IsDisposed)
                        {
                            Vector2 drawPos = tile.Position + new Vector2(offsetX, offsetY);
                            _spriteBatch.Draw(tile.Texture, drawPos, tile.Source, XnaColor.White, 0f, Vector2.Zero, 1f, tile.Effects, 0f);
                        }
                    }
                }
            }
        }
    }
}
