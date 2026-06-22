using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Rendering2D;

public static class PlaceholderPlayerRenderer
{
	private static Query<Position, PreviousPosition, CharacterClass, FacingDirection> _playerQuery;
	private static SpriteBatch _batchCache = null!;
	private static float _alphaCache;

	public static void Initialize(World world)
	{
		_playerQuery = world.QueryBuilder<Position, PreviousPosition, CharacterClass, FacingDirection>().Build();
	}

	public static void Draw(SpriteBatch spriteBatch, float alpha)
	{
		_batchCache = spriteBatch;
		_alphaCache = alpha;

		_playerQuery.Each((Entity e, ref Position pos, ref PreviousPosition prevPos, ref CharacterClass cClass, ref FacingDirection facing) =>
		{
			Color renderColor = cClass.Id == 0 ? Color.Orange : Color.Cyan;
			if (e.Has<RemotePlayerTag>()) renderColor = Color.LightSkyBlue;

			float renderX = MathHelper.Lerp(prevPos.X, pos.X, _alphaCache);
			float renderY = MathHelper.Lerp(prevPos.Y, pos.Y, _alphaCache);

			SpriteEffects fx = facing.Value < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			// ARCHITECTURE FIX: Render footprint dynamically adapts to the ECS Perspective Tag
			Rectangle drawRect = e.Has<TopDownTag>()
				? new Rectangle((int)renderX - 8, (int)renderY - 8, 8, 8)   // Top-Down Square
				: new Rectangle((int)renderX - 5, (int)renderY - 12, 10, 24); // Sidescroller Capsule

			_batchCache.Draw(
				AssetManager.WhitePixel,
				drawRect,
				null,
				renderColor,
				0f,
				Vector2.Zero,
				fx,
				0f
			);
		});
	}
}
