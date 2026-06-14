using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MyGame.Engine.States;

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

	public abstract void Update(GameTime gameTime);
	public abstract void Draw(SpriteBatch spriteBatch);
}
