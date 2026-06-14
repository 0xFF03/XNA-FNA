using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MyGame.Engine.States;

public class StateManager
{
	private readonly List<GameState> stateStack = new();
	private readonly List<System.Action> pendingOperations = new();

	public static StateManager Instance { get; private set; } = null!;

	public StateManager()
	{
		Instance = this;
	}

	public void PushState(GameState state)
	{
		pendingOperations.Add(() =>
		{
			stateStack.Add(state);
			state.LoadContent();
		});
	}

	public void PopState()
	{
		pendingOperations.Add(() =>
		{
			if (stateStack.Count > 0)
			{
				stateStack[^1].UnloadContent();
				stateStack.RemoveAt(stateStack.Count - 1);
			}
		});
	}

	public void ChangeState(GameState state)
	{
		pendingOperations.Add(() =>
		{
			foreach (var existingState in stateStack)
			{
				existingState.UnloadContent();
			}

			stateStack.Clear();
			stateStack.Add(state);
			state.LoadContent();
		});
	}

	public void Update(GameTime gameTime)
	{
		foreach (var op in pendingOperations) op();
		pendingOperations.Clear();

		if (stateStack.Count == 0) return;
		stateStack[^1].Update(gameTime);
	}

	public void Draw(SpriteBatch spriteBatch)
	{
		if (stateStack.Count == 0) return;
		foreach (var state in stateStack)
		{
			state.Draw(spriteBatch);
		}
	}
}
