using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MyGame.Engine.UI;

public class Button
{
	private readonly Texture2D texture;
	private readonly Rectangle bounds;
	private MouseState currentMouse;
	private MouseState previousMouse;

	public event Action? OnClick;

	public Button(Texture2D texture, Vector2 position)
	{
		this.texture = texture;
		bounds = new Rectangle((int)position.X, (int)position.Y, texture.Width, texture.Height);
	}

	public void Update()
	{
		previousMouse = currentMouse;
		currentMouse = Mouse.GetState();

		// Check if the mouse is currently hovering over the button bounds
		if (bounds.Contains(currentMouse.X, currentMouse.Y))
		{
			// Trigger ONLY when the player lifts their finger off the button
			if (currentMouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
			{
				OnClick?.Invoke();
			}
		}
	}

	public void Draw(SpriteBatch spriteBatch, Color? overrideColor = null)
	{
		Color tint = overrideColor ?? (bounds.Contains(currentMouse.X, currentMouse.Y) ? Color.LightGray : Color.White);
		spriteBatch.Draw(texture, bounds, tint);
	}
}
