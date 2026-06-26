using System;
using Flecs.NET.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Game.Core;
using MyGame.Engine.StandardModules.Multiplayer;

namespace MyGame.Engine.StandardModules.Rendering2D;

public static class PlaceholderVehicleRenderer
{
	private static Query<Position, PreviousPosition, ShipVehicleComponent, PhysicsDimension, NetworkId> _vehicleQuery;

	public static void Initialize(World world)
	{
		_vehicleQuery = world.QueryBuilder<Position, PreviousPosition, ShipVehicleComponent, PhysicsDimension, NetworkId>().Build();
	}

	public static void Draw(SpriteBatch spriteBatch, float alpha, string activeDimension, ulong excludeNetId = 0)
	{
		_vehicleQuery.Each((ref Position pos, ref PreviousPosition prevPos, ref ShipVehicleComponent ship, ref PhysicsDimension dim, ref NetworkId netId) =>
		{
			if (dim.Name != activeDimension) return;

			if (excludeNetId != 0 && netId.Value == excludeNetId) return;

			Texture2D shipTex = AssetManager.GetTexture(ship.TextureName);
			if (shipTex == null || shipTex.IsDisposed) return;

			// ARCHITECTURE FIX: Strict sub-pixel integer rounding locks the ship visual to the un-aliased monitor grid
			float renderX = MathF.Round(MathHelper.Lerp(prevPos.X, pos.X, alpha));
			float renderY = MathF.Round(MathHelper.Lerp(prevPos.Y, pos.Y, alpha));
			float renderRot = MathHelper.WrapAngle(MathHelper.Lerp(prevPos.Rotation, pos.Rotation, alpha));

			Vector2 originOffset = new Vector2(MathF.Round(shipTex.Width / 2f), MathF.Round(shipTex.Height / 2f));
			Vector2 drawPosition = new Vector2(renderX, renderY);

			spriteBatch.Draw(shipTex, drawPosition, null, Color.White, renderRot, originOffset, 1f, SpriteEffects.None, 0f);
		});
	}
}
