using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;

// Universal Headers
using MyGame.Prefabs;
using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.Game.Renderers;

public static class ProjectileRenderSystem
{
	private static Query<Position, PreviousPosition> _projectileQuery;
	private static SpriteBatch _batchCache = null!;
	private static float _alphaCache;

	public static void Initialize(World world)
	{
		_projectileQuery = world.QueryBuilder<Position, PreviousPosition>().With<ProjectileTag>().Build();
	}

	public static void Draw(SpriteBatch spriteBatch, float alpha)
	{
		_batchCache = spriteBatch;
		_alphaCache = alpha;

		_projectileQuery.Each((ref Position pos, ref PreviousPosition prevPos) =>
		{
			// ARCHITECTURE FIX: Interpolation between physics ticks
			float renderX = MathHelper.Lerp(prevPos.X, pos.X, _alphaCache);
			float renderY = MathHelper.Lerp(prevPos.Y, pos.Y, _alphaCache);

			_batchCache.Draw(
				AssetManager.WhitePixel,
				new Rectangle((int)renderX - 4, (int)renderY - 4, 8, 8),
				XnaColor.Yellow
			);
		});
	}
}
