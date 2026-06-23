using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Game.Core;

namespace MyGame.Engine.StandardModules.Rendering2D;

public static class PlaceholderVehicleRenderer
{
	private static Query<Position, PreviousPosition, ShipVehicleComponent, PhysicsDimension> _vehicleQuery;

	public static void Initialize(World world)
	{
		_vehicleQuery = world.QueryBuilder<Position, PreviousPosition, ShipVehicleComponent, PhysicsDimension>().Build();
	}

	public static void Draw(SpriteBatch spriteBatch, float alpha, string activeDimension)
	{
		_vehicleQuery.Each((ref Position pos, ref PreviousPosition prevPos, ref ShipVehicleComponent ship, ref PhysicsDimension dim) =>
		{
			if (dim.Name != activeDimension) return;

			Texture2D shipTex = AssetManager.GetTexture(ship.TextureName);
			if (shipTex == null || shipTex.IsDisposed) return;

			float renderX = MathHelper.Lerp(prevPos.X, pos.X, alpha);
			float renderY = MathHelper.Lerp(prevPos.Y, pos.Y, alpha);

			Vector2 originOffset = new Vector2(shipTex.Width / 2f, shipTex.Height / 2f);
			Vector2 drawPosition = new Vector2(renderX, renderY);

			spriteBatch.Draw(shipTex, drawPosition, null, Color.White, 0f, originOffset, 1f, SpriteEffects.None, 0f);
		});
	}
}
