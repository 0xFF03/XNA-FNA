using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Platform;
using MyGame.Game.Core;
using System;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.Engine.StandardModules.Rendering2D;

public static class TileMapRenderer
{
    private static SpriteBatch _spriteBatch = null!;
    private static Query<MapComponents.MapInstance> _mapQuery;

    public static void Initialize(World world, SpriteBatch batch)
    {
        _spriteBatch = batch;
        _mapQuery = world.QueryBuilder<MapComponents.MapInstance>().Build();
    }

    public static void Draw(Camera2D camera, int virtualWidth, int virtualHeight)
    {
        Vector2 clampedCamPos = camera.GetClampedPosition(virtualWidth, virtualHeight);
        float zoom = camera.Zoom <= 0f ? 1f : camera.Zoom;

        float viewLeft = clampedCamPos.X - (virtualWidth / 2f / zoom) - 32f;
        float viewRight = clampedCamPos.X + (virtualWidth / 2f / zoom) + 32f;
        float viewTop = clampedCamPos.Y - (virtualHeight / 2f / zoom) - 32f;
        float viewBottom = clampedCamPos.Y + (virtualHeight / 2f / zoom) + 32f;

        int minChunkX = (int)(viewLeft / LevelData.ChunkSize);
        int maxChunkX = (int)(viewRight / LevelData.ChunkSize);
        int minChunkY = (int)(viewTop / LevelData.ChunkSize);
        int maxChunkY = (int)(viewBottom / LevelData.ChunkSize);

        _mapQuery.Each((ref MapComponents.MapInstance mapInstance) =>
        {
            var roomData = mapInstance.Data;

            for (int cy = minChunkY; cy <= maxChunkY; cy++)
            {
                for (int cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    Point targetChunk = new Point(cx, cy);
                    if (roomData.TileChunks.TryGetValue(targetChunk, out var chunkTiles))
                    {
                        foreach (var tile in chunkTiles)
                        {
                            if (tile.Texture != null && !tile.Texture.IsDisposed)
                            {
                                _spriteBatch.Draw(tile.Texture, tile.Position, tile.Source, XnaColor.White, 0f, Vector2.Zero, 1f, tile.Effects, 0f);
                            }
                        }
                    }
                }
            }
        });
    }
}
