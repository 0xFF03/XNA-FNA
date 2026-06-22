using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.UI;
using MyGame.Game.Logic;

namespace MyGame.Game.UIStates;

public class MainMenuState : GameState
{
    private Button startButton = null!;
    private Button loadButton = null!;
    private Button optionsButton = null!;
    private Button quitButton = null!;

    public MainMenuState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

    public override void LoadContent()
    {
        Texture2D uiTex = AssetManager.WhitePixel;

        startButton = new Button(uiTex, Rectangle.Empty) { NormalColor = Color.DarkGreen, HoverColor = Color.Green };
        loadButton = new Button(uiTex, Rectangle.Empty) { Text = "Load Save", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        optionsButton = new Button(uiTex, Rectangle.Empty) { Text = "Options", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        quitButton = new Button(uiTex, Rectangle.Empty) { Text = "Quit Game", NormalColor = Color.DarkRed, HoverColor = Color.Red };

        startButton.OnClick += () =>
        {
            startButton.IsEnabled = false;
            if (SaveManager.HasSaves)
            {
                var latest = SaveManager.GetLatestProfile();
                if (latest != null)
                {
                    SaveManager.LoadProfile(latest.Id);
                    stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld));
                    return;
                }
            }
            stateManager.ChangeState(new CharacterSelectState(game, stateManager));
        };

        loadButton.OnClick += () => stateManager.PushState(new LoadSaveState(game, stateManager));
        optionsButton.OnClick += () => stateManager.PushState(new OptionsState(game, stateManager));
        quitButton.OnClick += () => game.Exit();
    }

    public override void Update(float deltaTime)
    {
        // ARCHITECTURE FIX: Instantly dynamic UI without DB performance hit
        bool hasSaves = SaveManager.HasSaves;
        startButton.Text = hasSaves ? "Continue" : "New Game";
        loadButton.IsEnabled = hasSaves;

        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 125;
        int startY = (viewport.Height / 2) - 100;
        int spacing = 55;

        // Clean, uniform vertical arrangement
        startButton.Bounds = new Rectangle(centerX, startY, 250, 45);
        loadButton.Bounds = new Rectangle(centerX, startY + spacing, 250, 45);
        optionsButton.Bounds = new Rectangle(centerX, startY + spacing * 2, 250, 45);
        quitButton.Bounds = new Rectangle(centerX, startY + spacing * 3, 250, 45);

        Point mousePos = InputManager.GetScreenMousePosition();
        bool isClicked = InputManager.ConsumeUIClick();

        startButton.Update(mousePos, isClicked);
        loadButton.Update(mousePos, isClicked);
        optionsButton.Update(mousePos, isClicked);
        quitButton.Update(mousePos, isClicked);
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        game.GraphicsDevice.Clear(Color.SlateGray);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

        startButton.Draw(spriteBatch);
        loadButton.Draw(spriteBatch);
        optionsButton.Draw(spriteBatch);
        quitButton.Draw(spriteBatch);

        spriteBatch.End();
    }
}
