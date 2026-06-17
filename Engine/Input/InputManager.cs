using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace MyGame.Engine.Input;

public static class GameActions
{
    public const string MoveUp = "MoveUp";
    public const string MoveDown = "MoveDown";
    public const string MoveLeft = "MoveLeft";
    public const string MoveRight = "MoveRight";
    public const string Jump = "Jump";
    public const string Pause = "Pause";
    public const string Interact = "Interact";
}

public static class InputManager
{
    private static KeyboardState currentKeyboard;
    private static KeyboardState previousKeyboard;
    private static MouseState currentMouse;
    private static MouseState previousMouse;

    private static readonly HashSet<string> _actionBuffer = new();
    private static bool _uiClickBuffer = false;
    private static bool _blockInputUntilRelease = false;

    private static readonly Dictionary<string, Keys> KeyBindings = new()
    {
        { GameActions.MoveUp, Keys.W }, { GameActions.MoveDown, Keys.S },
        { GameActions.MoveLeft, Keys.A }, { GameActions.MoveRight, Keys.D },
        { GameActions.Jump, Keys.Space }, { GameActions.Pause, Keys.Escape },
        { GameActions.Interact, Keys.E }
    };

    public static void Update()
    {
        previousKeyboard = currentKeyboard;
        currentKeyboard = Keyboard.GetState();
        previousMouse = currentMouse;
        currentMouse = Mouse.GetState();

        _uiClickBuffer = false;

        if (_blockInputUntilRelease && currentMouse.LeftButton == ButtonState.Released && currentKeyboard.GetPressedKeys().Length == 0)
        {
            _blockInputUntilRelease = false;
        }

        foreach (var kvp in KeyBindings)
        {
            if (currentKeyboard.IsKeyDown(kvp.Value) && previousKeyboard.IsKeyUp(kvp.Value))
                _actionBuffer.Add(kvp.Key);
        }

        if (!_blockInputUntilRelease && currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            _uiClickBuffer = true;
        }
    }

    // ARCHITECTURE FIX: Essential for Linux/Proton focus-loss scenarios
    public static void ResetState()
    {
        currentKeyboard = Keyboard.GetState();
        previousKeyboard = currentKeyboard;
        _actionBuffer.Clear();
        _blockInputUntilRelease = true;
    }

    public static bool IsActionActive(string action) => !_blockInputUntilRelease && KeyBindings.TryGetValue(action, out Keys key) && currentKeyboard.IsKeyDown(key);

    public static bool ConsumeAction(string action)
    {
        if (_actionBuffer.Contains(action))
        {
            _actionBuffer.Remove(action);
            return true;
        }
        return false;
    }

    public static bool ConsumeUIClick()
    {
        if (_uiClickBuffer)
        {
            _uiClickBuffer = false;
            return true;
        }
        return false;
    }

    public static Point GetMousePosition() => new Point(currentMouse.X, currentMouse.Y);

    public static void Clear()
    {
        currentKeyboard = Keyboard.GetState();
        previousKeyboard = currentKeyboard;
        currentMouse = Mouse.GetState();
        previousMouse = currentMouse;

        _actionBuffer.Clear();
        _uiClickBuffer = false;
        _blockInputUntilRelease = true;
    }
}
