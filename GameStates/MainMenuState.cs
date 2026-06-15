using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.UI;
using MyGame.Engine.Core;
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
        Texture2D uiTex = AssetManager.WhitePixel;

        startButton = new Button(uiTex, Rectangle.Empty)
        {
            Text = "Start Game", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue
        };

        // ARCHITECTURE FIX: Await lobby readiness to prevent race-condition flashes
        startButton.OnClick += async () =>
        {
            startButton.IsEnabled = false;
            startButton.Text = "Creating Lobby...";

            await SteamManager.CreateLobby();

            if (SteamManager.CurrentLobby.HasValue)
            {
                stateManager.ChangeState(new CharacterSelectState(game, stateManager));
            }
            else
            {
                startButton.IsEnabled = true;
                startButton.Text = "Start Game";
            }
        };

        optionsButton = new Button(uiTex, Rectangle.Empty)
        {
            Text = "Options", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue
        };
        optionsButton.OnClick += () =>
        {
            stateManager.PushState(new OptionsState(game, stateManager));
        };

        quitButton = new Button(uiTex, Rectangle.Empty)
        {
            Text = "Quit Game", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue
        };
        quitButton.OnClick += () => game.Exit();
    }

    public override void Update(GameTime gameTime)
    {
        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 100;
        int startY = (viewport.Height / 2) - 80;

        startButton.Bounds = new Rectangle(centerX, startY, 200, 50);
        optionsButton.Bounds = new Rectangle(centerX, startY + 70, 200, 50);
        quitButton.Bounds = new Rectangle(centerX, startY + 140, 200, 50);

        startButton.Update();
        optionsButton.Update();
        quitButton.Update();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        game.GraphicsDevice.Clear(Color.SlateGray);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

        startButton.Draw(spriteBatch);
        optionsButton.Draw(spriteBatch);
        quitButton.Draw(spriteBatch);

        spriteBatch.End();
    }
}
