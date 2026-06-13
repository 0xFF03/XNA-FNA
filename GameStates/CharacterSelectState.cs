using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.UI;
using MyGame.Engine.Networking;

namespace MyGame.GameStates;

public class CharacterSelectState : GameState
{
    private Button startRunButton = null!;
    private Button inviteButton = null!;
    private Button backButton = null!;

    public CharacterSelectState(Game1 game, StateManager stateManager) : base(game, stateManager) { }

    public override void LoadContent()
    {
        Texture2D tex = new Texture2D(game.GraphicsDevice, 200, 50);
        Color[] data = new Color[200 * 50];
        System.Array.Fill(data, Color.DarkGreen);
        tex.SetData(data);

        // Staggered layout coordinates to ensure zero input collision errors
        startRunButton = new Button(tex, new Vector2(100, 200));
        inviteButton = new Button(tex, new Vector2(100, 280));
        backButton = new Button(tex, new Vector2(100, 360));

        startRunButton.OnClick += () =>
        {
            Console.WriteLine("[UI Click]: 'Start Run' button pressed in Character Select. Launching game world simulation.");
            stateManager.ChangeState(new GameplayState(game, stateManager));
        };

        inviteButton.OnClick += () =>
        {
            Console.WriteLine("[UI Click]: 'Invite Friends' button pressed in Character Select. Initializing Steam Friends Overlay.");
            SteamManager.OpenInviteOverlay();
        };

        backButton.OnClick += () =>
        {
            Console.WriteLine("[UI Click]: 'Back' button pressed in Character Select. Returning to Main Menu slate.");
            stateManager.ChangeState(new MainMenuState(game, stateManager));
        };
    }

    public override void Update(GameTime gameTime)
    {
        startRunButton.Update();
        inviteButton.Update();
        backButton.Update();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        game.GraphicsDevice.Clear(Color.DarkSlateGray);

        startRunButton.Draw(spriteBatch);
        inviteButton.Draw(spriteBatch);
        backButton.Draw(spriteBatch);
    }
}
