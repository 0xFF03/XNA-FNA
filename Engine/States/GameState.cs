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

	// NEW: Base method allowing any state to safely clean up unmanaged memory
	public virtual void UnloadContent() { }

	public abstract void Update(GameTime gameTime);
	public abstract void Draw(SpriteBatch spriteBatch);
}
