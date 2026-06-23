using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Rendering2D;

public static class PlaceholderProjectileRenderer
{
	private static Query<Position, PreviousPosition, PhysicsDimension> _projectileQuery;

	public static void Initialize(World world)
	{
		_projectileQuery = world.QueryBuilder<Position, PreviousPosition, PhysicsDimension>().With<BaseCombatComponents.ProjectileTag>().Build();
	}

	// Inside PlaceholderProjectileRenderer.cs:
	public static void Draw(SpriteBatch spriteBatch, float alpha, string activeDimension)
	{
		_projectileQuery.Each((ref Position pos, ref PreviousPosition prevPos, ref PhysicsDimension dim) =>
		{
			if (dim.Name != activeDimension) return;

			float renderX = MathHelper.Lerp(prevPos.X, pos.X, alpha);
			float renderY = MathHelper.Lerp(prevPos.Y, pos.Y, alpha);
			Vector2 drawPos = new Vector2(renderX, renderY);

			// Vector2 floating point scaling for perfect sub-pixel rendering
			spriteBatch.Draw(AssetManager.WhitePixel, drawPos, null, Color.Yellow, 0f, new Vector2(0.5f, 0.5f), new Vector2(8f, 8f), SpriteEffects.None, 0f);
		});
	}
}
