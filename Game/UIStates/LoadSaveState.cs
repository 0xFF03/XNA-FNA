using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.UI;
using MyGame.Game.Logic;

namespace MyGame.Game.UIStates;

public class LoadSaveState : GameState
{
    private readonly Button[] slotButtons = new Button[10];
    private Button backButton = null!;

    public LoadSaveState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

    public override void LoadContent()
    {
        Texture2D uiTex = AssetManager.WhitePixel;
        var profiles = SaveManager.GetDisplayProfiles();

        int viewportHeight = game.GraphicsDevice.Viewport.Height;
        int centerX = (game.GraphicsDevice.Viewport.Width / 2) - 200;
        int startY = (viewportHeight / 2) - 260; // Start higher to fit 10 slots vertically

        for (int i = 0; i < 10; i++)
        {
            int slotId = i + 1;
            var p = profiles[i];
            bool isAutoSave = i < 3;

            slotButtons[i] = new Button(uiTex, new Rectangle(centerX, startY + (i * 45), 400, 40))
            {
                NormalColor = Color.DarkSlateBlue,
                HoverColor = Color.SlateBlue,
                FontSize = 16f
            };

            if (p == null)
            {
                slotButtons[i].Text = isAutoSave ? $"Auto-Save {slotId} - Empty" : $"Slot {slotId} - Empty";
                slotButtons[i].IsEnabled = false;
            }
            else
            {
                TimeSpan t = TimeSpan.FromSeconds(p.TotalPlayTimeSeconds);
                string prefix = isAutoSave ? "Auto" : "Slot";
                slotButtons[i].Text = $"{prefix} {slotId} | {p.LastSaved:MM/dd HH:mm} | {t.Hours}h {t.Minutes}m";
                slotButtons[i].IsEnabled = true;

                slotButtons[i].OnClick += () =>
                {
                    SaveManager.LoadProfile(slotId);
                    stateManager.ChangeState(new GameplayState(game, stateManager, game.EcsWorld));
                };
            }
        }

        backButton = new Button(uiTex, new Rectangle(centerX, startY + 460, 400, 40))
        {
            Text = "Back to Menu",
            NormalColor = Color.DarkRed,
            HoverColor = Color.Red
        };

        backButton.OnClick += () => stateManager.PopState();
    }

    public override void Update(float deltaTime)
    {
        Point mousePos = InputManager.GetScreenMousePosition();
        bool isClicked = InputManager.ConsumeUIClick();

        for (int i = 0; i < 10; i++) slotButtons[i].Update(mousePos, isClicked);
        backButton.Update(mousePos, isClicked);
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        game.GraphicsDevice.Clear(Color.DimGray);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

        for (int i = 0; i < 10; i++) slotButtons[i].Draw(spriteBatch);
        backButton.Draw(spriteBatch);

        spriteBatch.End();
    }
}
