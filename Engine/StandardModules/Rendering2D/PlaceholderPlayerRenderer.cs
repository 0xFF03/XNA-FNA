using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Rendering2D;

public static class PlaceholderPlayerRenderer
{
	private static Query<Position, PreviousPosition, CharacterClass, FacingDirection, PhysicsDimension> _playerQuery;

	public static void Initialize(World world)
	{
		_playerQuery = world.QueryBuilder<Position, PreviousPosition, CharacterClass, FacingDirection, PhysicsDimension>().Build();
	}

	public static void Draw(SpriteBatch spriteBatch, float alpha, string activeDimension)
	{
		_playerQuery.Each((Entity e, ref Position pos, ref PreviousPosition prevPos, ref CharacterClass cClass, ref FacingDirection facing, ref PhysicsDimension dim) =>
		{
			if (dim.Name != activeDimension) return;

			Color renderColor = cClass.Id == 0 ? Color.Orange : Color.Cyan;
			if (e.Has<RemotePlayerTag>()) renderColor = Color.LightSkyBlue;

			// ARCHITECTURE FIX: Floating point precision maintained for perfectly smooth 144Hz movement
			float renderX = MathHelper.Lerp(prevPos.X, pos.X, alpha);
			float renderY = MathHelper.Lerp(prevPos.Y, pos.Y, alpha);
			Vector2 drawPos = new Vector2(renderX, renderY);

			SpriteEffects fx = facing.Value < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			Vector2 origin;
			Vector2 scale;

			if (e.Has<TopDownTag>())
			{
				origin = new Vector2(0.5f, 0.5f); // Center of a 1x1 pixel
				scale = new Vector2(8f, 8f);
			}
			else
			{
				origin = new Vector2(0.5f, 0.5f);
				scale = new Vector2(10f, 24f);
			}

			spriteBatch.Draw(AssetManager.WhitePixel, drawPos, null, renderColor, 0f, origin, scale, fx, 0f);
		});
	}
}
