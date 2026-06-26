using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.Platform.UI;
using MyGame.Engine.Platform.Networking;

namespace MyGame.Game.UIStates;

public class FriendsListOverlay
{
    public bool IsVisible { get; set; } = false;

    private readonly Game1 _game;
    private readonly List<NetworkFriend> _friendsList = new();
    private readonly Button[] _friendButtons;
    private readonly Button _nextButton;
    private readonly Button _prevButton;
    private readonly Button _closeButton;

    private int _currentPage = 0;
    private const int FriendsPerPage = 5;

    public FriendsListOverlay(Game1 game)
    {
        _game = game;
        Texture2D uiTex = AssetManager.WhitePixel;

        _friendButtons = new Button[FriendsPerPage];
        for (int i = 0; i < FriendsPerPage; i++)
        {
            int index = i;
            _friendButtons[i] = new Button(uiTex, Rectangle.Empty) { HasIconSlot = true, NormalColor = Color.DarkSlateGray, HoverColor = Color.LightSlateGray };
            _friendButtons[i].OnClick += () => InviteFriendAtIndex(index);
        }

        _prevButton = new Button(uiTex, Rectangle.Empty) { Text = "<", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        _nextButton = new Button(uiTex, Rectangle.Empty) { Text = ">", NormalColor = Color.DarkSlateBlue, HoverColor = Color.SlateBlue };
        _closeButton = new Button(uiTex, Rectangle.Empty) { Text = "Close", NormalColor = Color.DarkRed, HoverColor = Color.Red };

        _prevButton.OnClick += () => { if (_currentPage > 0) _currentPage--; };
        _nextButton.OnClick += () => { if ((_currentPage + 1) * FriendsPerPage < _friendsList.Count) _currentPage++; };
        _closeButton.OnClick += () => { IsVisible = false; };
    }

    public void Show()
    {
        IsVisible = true;
        _currentPage = 0;
        _friendsList.Clear();

        var net = NetworkServiceLocator.Provider;
        if (net.IsActive)
        {
            _friendsList.AddRange(net.GetFriends().Where(f => f.IsOnline));
        }
    }

    private void InviteFriendAtIndex(int buttonIndex)
    {
        int actualIndex = (_currentPage * FriendsPerPage) + buttonIndex;
        if (actualIndex < _friendsList.Count)
        {
            NetworkServiceLocator.Provider.InviteFriend(_friendsList[actualIndex].Id);
        }
    }

    public void Update(Point mousePos, bool isClicked)
    {
        if (!IsVisible) return;

        var viewport = _game.GraphicsDevice.Viewport;
        int width = 400;
        int height = 450;
        int startX = (viewport.Width - width) / 2;
        int startY = (viewport.Height - height) / 2;

        var net = NetworkServiceLocator.Provider;

        for (int i = 0; i < FriendsPerPage; i++)
        {
            int actualIndex = (_currentPage * FriendsPerPage) + i;
            if (actualIndex < _friendsList.Count)
            {
                var friend = _friendsList[actualIndex];
                _friendButtons[i].Text = friend.Name;

                // Set Icon to null to fully decouple UI from Facepunch avatar structs
                _friendButtons[i].Icon = null;
                _friendButtons[i].Bounds = new Rectangle(startX + 20, startY + 40 + (i * 60), width - 40, 50);

                if (net.IsInLobby && net.ActivePeers.Contains(friend.Id))
                {
                    _friendButtons[i].Text = friend.Name + " (In Lobby)";
                    _friendButtons[i].IsEnabled = false;
                }
                else _friendButtons[i].IsEnabled = true;

                _friendButtons[i].Update(mousePos, isClicked);
            }
        }

        _prevButton.IsEnabled = _currentPage > 0;
        _nextButton.IsEnabled = (_currentPage + 1) * FriendsPerPage < _friendsList.Count;

        _prevButton.Bounds = new Rectangle(startX + 20, startY + height - 80, 50, 45);
        _nextButton.Bounds = new Rectangle(startX + width - 70, startY + height - 80, 50, 45);
        _closeButton.Bounds = new Rectangle(startX + 80, startY + height - 80, width - 160, 45);

        _prevButton.Update(mousePos, isClicked);
        _nextButton.Update(mousePos, isClicked);
        _closeButton.Update(mousePos, isClicked);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible) return;

        var viewport = _game.GraphicsDevice.Viewport;
        int width = 400;
        int height = 450;
        int startX = (viewport.Width - width) / 2;
        int startY = (viewport.Height - height) / 2;

        spriteBatch.Draw(AssetManager.WhitePixel, new Rectangle(startX, startY, width, height), Color.Black * 0.95f);

        for (int i = 0; i < FriendsPerPage; i++)
        {
            int actualIndex = (_currentPage * FriendsPerPage) + i;
            if (actualIndex < _friendsList.Count) _friendButtons[i].Draw(spriteBatch);
        }

        _prevButton.Draw(spriteBatch);
        _nextButton.Draw(spriteBatch);
        _closeButton.Draw(spriteBatch);
    }
}
