using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Rendering;

// Universal Headers
using MyGame.Prefabs;
using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;
using MyGame.Game.Environment; // <-- FIXED PATH

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.Game.Renderers;

public static class TileRenderSystem
{
    private static SpriteBatch _spriteBatch = null!;
    private static Query<MapInstance> _mapQuery;

    public static void Initialize(World world, SpriteBatch batch)
    {
        _spriteBatch = batch;
        _mapQuery = world.QueryBuilder<MapInstance>().Build();
    }

    public static void Draw(World world, Camera2D camera, int virtualWidth, int virtualHeight)
    {
        Vector2 clampedCamPos = camera.GetClampedPosition(virtualWidth, virtualHeight);
        float zoom = camera.Zoom <= 0f ? 1f : camera.Zoom;

        float viewLeft = clampedCamPos.X - (virtualWidth / 2f / zoom) - 64f;
        float viewRight = clampedCamPos.X + (virtualWidth / 2f / zoom) + 64f;
        float viewTop = clampedCamPos.Y - (virtualHeight / 2f / zoom) - 64f;
        float viewBottom = clampedCamPos.Y + (virtualHeight / 2f / zoom) + 64f;

        _mapQuery.Each((ref MapInstance mapInstance) =>
        {
            var roomData = mapInstance.Data;
            for (int i = 0; i < roomData.Tiles.Length; i++)
            {
                var tile = roomData.Tiles[i];

                if (tile.Texture != null && !tile.Texture.IsDisposed)
                {
                    if (tile.Position.X >= viewLeft && tile.Position.X <= viewRight &&
                        tile.Position.Y >= viewTop && tile.Position.Y <= viewBottom)
                    {
                        _spriteBatch.Draw(tile.Texture, tile.Position, tile.Source, XnaColor.White, 0f, Vector2.Zero, 1f, tile.Effects, 0f);
                    }
                }
            }
        });
    }
}
