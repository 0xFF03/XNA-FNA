using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.UI;

namespace MyGame.GameStates;

public class PauseMenuState : GameState
{
	private Button resumeButton = null!;

	public PauseMenuState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

	public override void LoadContent()
	{
		Texture2D texture = new Texture2D(game.GraphicsDevice, 200, 50);
		Color[] data = new Color[200 * 50];
		Array.Fill(data, Color.DarkRed);
		texture.SetData(data);

		resumeButton = new Button(texture, new Vector2(540, 330));
		resumeButton.OnClick += () =>
		{
			// NETWORK TODO: Tell Steam P2P network we are unpausing
			stateManager.PopState();
		};
	}

	public override void Update(GameTime gameTime)
	{
		resumeButton.Update();
	}

	public override void Draw(SpriteBatch spriteBatch)
	{
		// Draw a dark, semi-transparent overlay so players can still see the game behind it
		Texture2D overlay = new Texture2D(game.GraphicsDevice, 1, 1);
		overlay.SetData(new[] { new Color(0, 0, 0, 150) });
		spriteBatch.Draw(overlay, new Rectangle(0, 0, 1280, 720), Color.White);

		resumeButton.Draw(spriteBatch);
	}
}
