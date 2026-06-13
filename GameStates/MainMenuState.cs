using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.UI;
using MyGame.Engine.Networking;

namespace MyGame.GameStates;

public class MainMenuState : GameState
{
    private Button startButton = null!;
    private Button optionsButton = null!;
    private Button quitButton = null!;

    public MainMenuState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

    public override void LoadContent()
    {
        Texture2D stdButton = CreateTexture(200, 50, Color.DarkSlateBlue);

        // 1. Start Game
        startButton = new Button(stdButton, new Vector2(540, 280));
        startButton.OnClick += () =>
        {
            Console.WriteLine("[UI Click]: 'Start Game' button pressed on Main Menu.");
            SteamManager.CreateLobby();
            stateManager.ChangeState(new CharacterSelectState(game, stateManager));
        };

        // 2. Options Button
        optionsButton = new Button(stdButton, new Vector2(540, 350));
        optionsButton.OnClick += () =>
        {
            Console.WriteLine("[UI Click]: 'Options' button pressed on Main Menu.");
            stateManager.PushState(new OptionsState(game, stateManager));
        };

        // 3. Quit Button
        quitButton = new Button(stdButton, new Vector2(540, 420));
        quitButton.OnClick += () =>
        {
            Console.WriteLine("[UI Click]: 'Quit Game' button pressed on Main Menu. Terminating process.");
            game.Exit();
        };
    }

    public override void Update(GameTime gameTime)
    {
        startButton.Update();
        optionsButton.Update();
        quitButton.Update();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        game.GraphicsDevice.Clear(Color.SlateGray);

        startButton.Draw(spriteBatch);
        optionsButton.Draw(spriteBatch);
        quitButton.Draw(spriteBatch);
    }

    private Texture2D CreateTexture(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(game.GraphicsDevice, width, height);
        Color[] data = new Color[width * height];
        System.Array.Fill(data, color);
        tex.SetData(data);
        return tex;
    }
}
