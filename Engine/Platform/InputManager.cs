using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace MyGame.Engine.Platform;

public static class GameActions
{
    public const string MoveUp = "MoveUp";
    public const string MoveDown = "MoveDown";
    public const string MoveLeft = "MoveLeft";
    public const string MoveRight = "MoveRight";
    public const string Jump = "Jump";
    public const string Pause = "Pause";
    public const string Interact = "Interact";
    public const string FlightToggle = "FlightToggle"; // ARCHITECTURE FIX: Native flight bind
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
        { GameActions.Interact, Keys.F }, { GameActions.FlightToggle, Keys.J }
    };

    public static void Update()
    {
        previousKeyboard = currentKeyboard;
        currentKeyboard = Keyboard.GetState();
        previousMouse = currentMouse;
        currentMouse = Mouse.GetState();

        if (_blockInputUntilRelease && currentMouse.LeftButton == ButtonState.Released)
        {
            bool anyBoundKeyPressed = false;
            foreach (var key in KeyBindings.Values)
            {
                if (currentKeyboard.IsKeyDown(key))
                {
                    anyBoundKeyPressed = true;
                    break;
                }
            }
            if (!anyBoundKeyPressed) _blockInputUntilRelease = false;
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

    public static void PostLogicUpdate()
    {
        _actionBuffer.Clear();
        _uiClickBuffer = false;
    }

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

    public static Point GetScreenMousePosition() => new Point(currentMouse.X, currentMouse.Y);

    public static Vector2 GetWorldMousePosition(Camera2D camera, int virtualWidth, int virtualHeight, int screenWidth, int screenHeight)
    {
        float scaleX = (float)screenWidth / virtualWidth;
        float scaleY = (float)screenHeight / virtualHeight;
        float scale = System.Math.Min(scaleX, scaleY);

        int newW = (int)(virtualWidth * scale);
        int newH = (int)(virtualHeight * scale);
        int offsetX = (screenWidth - newW) / 2;
        int offsetY = (screenHeight - newH) / 2;

        float virtualMouseX = (currentMouse.X - offsetX) / scale;
        float virtualMouseY = (currentMouse.Y - offsetY) / scale;

        Matrix inverseView = Matrix.Invert(camera.GetViewMatrix(virtualWidth, virtualHeight));
        return Vector2.Transform(new Vector2(virtualMouseX, virtualMouseY), inverseView);
    }

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
