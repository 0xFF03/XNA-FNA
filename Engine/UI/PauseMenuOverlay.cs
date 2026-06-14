using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyGame.Engine.States;
using MyGame.Engine.UI;
using MyGame.Engine.Networking;
using Steamworks;

using XnaColor = Microsoft.Xna.Framework.Color;

namespace MyGame.GameStates.UI;

public class PauseMenuOverlay
{
    public bool IsPaused { get; private set; } = false;

    private readonly Game1 game;
    private readonly StateManager stateManager;
    private readonly Texture2D buttonTexture;
    private readonly Texture2D overlayTexture;
    private readonly byte[] signalBuffer = new byte[1];

    private readonly Button continueButton;
    private readonly Button exitButton;

    private KeyboardState previousKeyboardState;

    public PauseMenuOverlay(Game1 game, StateManager stateManager)
    {
        this.game = game;
        this.stateManager = stateManager;

        buttonTexture = new Texture2D(game.GraphicsDevice, 200, 50);
        XnaColor[] btnData = new XnaColor[200 * 50];
        Array.Fill(btnData, XnaColor.DarkSlateGray);
        buttonTexture.SetData(btnData);

        overlayTexture = new Texture2D(game.GraphicsDevice, 1, 1);
        overlayTexture.SetData(new[] { XnaColor.FromNonPremultiplied(0, 0, 0, 180) });

        // No matter what resolution the player uses, the buttons will snap to the direct center.
        var viewport = game.GraphicsDevice.Viewport;
        int centerX = (viewport.Width / 2) - 100; // 100 is half the button width
        int startY = (viewport.Height / 2) - 60;

        continueButton = new Button(buttonTexture, new Vector2(centerX, startY));
        exitButton = new Button(buttonTexture, new Vector2(centerX, startY + 80));

        continueButton.OnClick += () => { TransmitPauseState(false); };
        exitButton.OnClick += () =>
        {
            SteamManager.LeaveLobby();
            stateManager.ChangeState(new MainMenuState(game, stateManager));
        };
    }

    public void Unload()
    {
        buttonTexture.Dispose();
        overlayTexture.Dispose();
    }

    public void Update()
    {
        var currentKeyboardState = Keyboard.GetState();
        ListenForNetworkSignals();

        if (currentKeyboardState.IsKeyDown(Keys.Escape) && previousKeyboardState.IsKeyUp(Keys.Escape))
        {
            TransmitPauseState(!IsPaused);
        }

        if (IsPaused)
        {
            while (SteamNetworking.IsP2PPacketAvailable(0)) SteamNetworking.ReadP2PPacket(0);

            continueButton.Update();
            exitButton.Update();
        }

        previousKeyboardState = currentKeyboardState;
    }

    private void ListenForNetworkSignals()
    {
        while (SteamNetworking.IsP2PPacketAvailable(1))
        {
            var packetData = SteamNetworking.ReadP2PPacket(1);
            if (packetData.HasValue && packetData.Value.Data.Length > 0)
            {
                byte signal = packetData.Value.Data[0];
                if (signal == 98) IsPaused = true;
                else if (signal == 97) IsPaused = false;
            }
        }
    }

    private void TransmitPauseState(bool enforcePause)
    {
        IsPaused = enforcePause;
        signalBuffer[0] = IsPaused ? (byte)98 : (byte)97;

        if (SteamManager.CurrentLobby.HasValue)
        {
            foreach (var member in SteamManager.CurrentLobby.Value.Members)
            {
                if (member.Id != SteamClient.SteamId)
                {
                    SteamNetworking.SendP2PPacket(member.Id, signalBuffer, 1, 1, P2PSend.Reliable);
                }
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (IsPaused)
        {
            spriteBatch.Draw(overlayTexture, game.GraphicsDevice.Viewport.Bounds, XnaColor.White);

            continueButton.Draw(spriteBatch);
            exitButton.Draw(spriteBatch);
        }
    }
}
