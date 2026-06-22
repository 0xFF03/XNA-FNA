using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace MyGame.Engine.Platform;

public class StateManager
{
	private readonly List<GameState> stateStack = new();
	private readonly List<Action> pendingOperations = new();

	public bool IsTransitioning { get; private set; } = false;

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
			InputManager.Clear();
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
				InputManager.Clear();
			}
		});
	}

	public void ChangeState(GameState state)
	{
		IsTransitioning = true;
		pendingOperations.Clear();

		pendingOperations.Add(() =>
		{
			foreach (var existingState in stateStack)
			{
				existingState.UnloadContent();
			}

			stateStack.Clear();
			stateStack.Add(state);
			state.LoadContent();

			InputManager.Clear();
			IsTransitioning = false;
		});
	}

	// ARCHITECTURE FIX: Direct float routing. Zero GC overhead.
	public void Update(float deltaTime)
	{
		foreach (var op in pendingOperations) op();
		pendingOperations.Clear();

		if (IsTransitioning || stateStack.Count == 0) return;

		stateStack[^1].Update(deltaTime);
	}

	public void Draw(SpriteBatch spriteBatch, float alpha)
	{
		if (IsTransitioning || stateStack.Count == 0) return;
		foreach (var state in stateStack)
		{
			state.Draw(spriteBatch, alpha);
		}
	}
}
