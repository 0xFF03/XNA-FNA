using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Rendering2D;

public struct PixelOverState
{
	public Texture2D Atlas;
	public object SkeletonData;
	public float AnimationTimer;
	public string CurrentClip;
}

public static class PixelOverRenderer
{
	private static Query<Position, PreviousPosition, FacingDirection, PixelOverState> _renderQuery;

	private static SpriteBatch _batchCache = null!;
	private static float _alphaCache;

	public static void Initialize(World world)
	{
		_renderQuery = world.QueryBuilder<Position, PreviousPosition, FacingDirection, PixelOverState>().Build();

		world.System<PixelOverState>("PixelOverAnimationAdvanceSystem")
			.Kind(Ecs.OnUpdate)
			.Each((Iter it, int i, ref PixelOverState state) =>
			{
				state.AnimationTimer += it.DeltaTime();
			});
	}

	public static void Draw(SpriteBatch spriteBatch, float alpha)
	{
		_batchCache = spriteBatch;
		_alphaCache = alpha;

		_renderQuery.Each((ref Position pos, ref PreviousPosition prevPos, ref FacingDirection facing, ref PixelOverState anim) =>
		{
			if (anim.Atlas == null || anim.Atlas.IsDisposed) return;

			float renderX = MathHelper.Lerp(prevPos.X, pos.X, _alphaCache);
			float renderY = MathHelper.Lerp(prevPos.Y, pos.Y, _alphaCache);
			Vector2 rootPosition = new Vector2(renderX, renderY);

			SpriteEffects fx = facing.Value < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
		});
	}
}
