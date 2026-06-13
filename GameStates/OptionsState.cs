using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.UI;

namespace MyGame.GameStates;

public class OptionsState : GameState
{
	private Button backButton = null!;

	public OptionsState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

	public override void LoadContent()
	{
		Texture2D tex = new Texture2D(game.GraphicsDevice, 200, 50);
		Color[] data = new Color[200 * 50];
		Array.Fill(data, Color.Black);
		tex.SetData(data);

		backButton = new Button(tex, new Vector2(540, 500));
		backButton.OnClick += () =>
		{
			Console.WriteLine("[UI Click]: 'Back' button pressed in Options Screen. Popping overlay state stack.");
			stateManager.PopState();
		};
	}

	public override void Update(GameTime gameTime)
	{
		backButton.Update();
	}

	public override void Draw(SpriteBatch spriteBatch)
	{
		game.GraphicsDevice.Clear(Color.DimGray);
		backButton.Draw(spriteBatch);
	}
}
