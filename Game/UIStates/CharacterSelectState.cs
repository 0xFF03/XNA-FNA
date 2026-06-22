using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.UI;
using MyGame.Game.Logic;

namespace MyGame.Game.UIStates;

public class CharacterSelectState : GameState
{
    private Button selectVanguardBtn = null!;
    private Button selectMageBtn = null!;
    private Button selectRangerBtn = null!;
    private Button backButton = null!;

    public CharacterSelectState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

    public override void LoadContent()
    {
        Texture2D uiTex = AssetManager.WhitePixel;

        selectVanguardBtn = new Button(uiTex, Rectangle.Empty) { Text = "Play Vanguard", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        selectMageBtn = new Button(uiTex, Rectangle.Empty) { Text = "Play Mage", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        selectRangerBtn = new Button(uiTex, Rectangle.Empty) { Text = "Play Ranger", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        backButton = new Button(uiTex, Rectangle.Empty) { Text = "Back", NormalColor = Color.DarkRed, HoverColor = Color.Red };

        selectVanguardBtn.OnClick += () => CreateAndStart(0);
        selectMageBtn.OnClick += () => CreateAndStart(1);
        selectRangerBtn.OnClick += () => CreateAndStart(2);

        backButton.OnClick += () => stateManager.ChangeState(new MainMenuState(game, stateManager));
    }

    private void CreateAndStart(int classId)
    {
        SaveManager.CreateNewProfile(1, "Solo Profile", classId, "Maps/Level1.ldtk");
        stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld));
    }

    public override void Update(float deltaTime)
    {
        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 125;
        int startY = (viewport.Height / 2) - 100;

        selectVanguardBtn.Bounds = new Rectangle(centerX, startY, 250, 45);
        selectMageBtn.Bounds = new Rectangle(centerX, startY + 60, 250, 45);
        selectRangerBtn.Bounds = new Rectangle(centerX, startY + 120, 250, 45);
        backButton.Bounds = new Rectangle(centerX, startY + 180, 250, 45);

        Point mousePos = InputManager.GetScreenMousePosition();
        bool isClicked = InputManager.ConsumeUIClick();

        selectVanguardBtn.Update(mousePos, isClicked);
        selectMageBtn.Update(mousePos, isClicked);
        selectRangerBtn.Update(mousePos, isClicked);
        backButton.Update(mousePos, isClicked);
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        game.GraphicsDevice.Clear(Color.DarkSlateGray);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

        selectVanguardBtn.Draw(spriteBatch);
        selectMageBtn.Draw(spriteBatch);
        selectRangerBtn.Draw(spriteBatch);
        backButton.Draw(spriteBatch);

        spriteBatch.End();
    }
}
