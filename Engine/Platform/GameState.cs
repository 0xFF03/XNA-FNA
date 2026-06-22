using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;

namespace MyGame.Engine.Platform;

public abstract class GameState
{
	protected Game1 game;
	protected StateManager stateManager;

	public GameState(Game1 game, StateManager stateManager)
	{
		this.game = game;
		this.stateManager = stateManager;
	}

	public abstract void LoadContent();

	public virtual void UnloadContent() { }

	public abstract void Update(float deltaTime);

	public abstract void Draw(SpriteBatch spriteBatch, float alpha = 1f);
}
