using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Rendering2D;

public static class PlaceholderVehicleRenderer
{
	private static Query<Position, PreviousPosition, ShipVehicleComponent> _vehicleQuery;
	private static SpriteBatch _batchCache = null!;
	private static float _alphaCache;

	public static void Initialize(World world)
	{
		_vehicleQuery = world.QueryBuilder<Position, PreviousPosition, ShipVehicleComponent>().Build();
	}

	public static void Draw(SpriteBatch spriteBatch, float alpha)
	{
		_batchCache = spriteBatch;
		_alphaCache = alpha;

		_vehicleQuery.Each((ref Position pos, ref PreviousPosition prevPos, ref ShipVehicleComponent ship) =>
		{
			Texture2D shipTex = AssetManager.GetTexture(ship.TextureName);
			if (shipTex == null || shipTex.IsDisposed) return;

			float renderX = MathHelper.Lerp(prevPos.X, pos.X, _alphaCache);
			float renderY = MathHelper.Lerp(prevPos.Y, pos.Y, _alphaCache);

			Vector2 originOffset = new Vector2(shipTex.Width / 2f, shipTex.Height / 2f);

			// ARCHITECTURE FIX: Render exactly at the physics coordinate to align with LDtk's center pivot
			Vector2 drawPosition = new Vector2(renderX, renderY);

			_batchCache.Draw(
				shipTex,
				drawPosition,
				null,
				Color.White,
				0f,
				originOffset,
				1f,
				SpriteEffects.None,
				0f
			);
		});
	}
}
