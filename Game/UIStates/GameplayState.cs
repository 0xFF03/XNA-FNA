using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.States;
using MyGame.Engine.Networking;
using MyGame.Engine.Rendering;
using MyGame.Engine.Core;
using Flecs.NET.Core;
using Steamworks;
using XnaColor = Microsoft.Xna.Framework.Color;

using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;
using MyGame.Game.Renderers;
using MyGame.Game.Environment;

namespace MyGame.Game.UIStates;

public class GameplayState : GameState
{
    private readonly Flecs.NET.Core.World ecsWorld;
    private readonly int selectedClassId;
    private readonly string targetMapPath;

    private PauseMenuOverlay pauseMenu = null!;
    private Camera2D camera = null!;
    private RenderTarget2D virtualRenderTarget = null!;

    public const int VirtualWidth = 640;
    public const int VirtualHeight = 360;
    private readonly bool isHostOrigin;

    private Query<Position, PreviousPosition> _localPlayerQuery;

    private static float _drawAlpha;
    private static Camera2D _drawCamera = null!;
    private static readonly List<Entity> _cleanupList = new(256);

    public bool IsSimulationPaused => pauseMenu != null && pauseMenu.IsPaused;

    public GameplayState(Game1 game, StateManager stateManager, Flecs.NET.Core.World sharedWorld, int chosenClassId, string mapPath)
        : base(game, stateManager)
    {
        ecsWorld = sharedWorld;
        selectedClassId = chosenClassId;
        targetMapPath = mapPath;
        isHostOrigin = !SteamManager.KnownHostId.HasValue || SteamManager.KnownHostId.Value == SteamClient.SteamId;
    }

    public override void LoadContent()
    {
        virtualRenderTarget = new RenderTarget2D(game.GraphicsDevice, VirtualWidth, VirtualHeight, false, SurfaceFormat.Color, DepthFormat.None);
        pauseMenu = new PauseMenuOverlay(game, stateManager, isHostOrigin);

        camera = new Camera2D();
        camera.Zoom = 1.0f;

        _localPlayerQuery = ecsWorld.QueryBuilder<Position, PreviousPosition>().With<LocalPlayerTag>().Build();

        if (isHostOrigin && SteamManager.IsSteamActive && SteamManager.CurrentLobby.HasValue)
        {
            SteamManager.CurrentLobby.Value.SetData("GameState", "InGame");
        }

        ecsWorld.Entity().Set(new MapLoadRequest { MapPath = targetMapPath, LocalClassId = selectedClassId });
    }

    public override void UnloadContent()
    {
        pauseMenu.Unload();

        _cleanupList.Clear();
        using var cleanupQuery = ecsWorld.QueryBuilder().With<MatchEntityTag>().Build();
        cleanupQuery.Each((Entity e) => { _cleanupList.Add(e); });

        for (int i = 0; i < _cleanupList.Count; i++)
        {
            if (_cleanupList[i].IsAlive())
            {
                _cleanupList[i].Destruct();
            }
        }
        _cleanupList.Clear();

        var mapEntity = ecsWorld.Entity("GlobalMapData");
        if (mapEntity.Has<MapInstance>())
        {
            mapEntity.Remove<MapInstance>();
        }

        virtualRenderTarget?.Dispose();
        AssetManager.UnloadLevelAssets();
        game.PhysicsWorld.Clear();
        NetworkRegistry.ClearAll();
    }

    public override void Update(GameTime gameTime)
    {
        if (!SteamManager.IsSteamActive || !SteamManager.CurrentLobby.HasValue)
        {
            if (!isHostOrigin)
            {
                SteamManager.LeaveLobby();
                stateManager.ChangeState(new MainMenuState(game, stateManager));
                return;
            }
        }

        pauseMenu.Update();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (!IsSimulationPaused)
        {
            game.PhysicsWorld.Step(dt);
            ecsWorld.Progress(dt);
        }
    }

    public override void Draw(SpriteBatch spriteBatch, float alpha = 1f)
    {
        _drawAlpha = alpha;
        _drawCamera = camera;

        _localPlayerQuery.Each((ref Position pos, ref PreviousPosition prevPos) =>
        {
            float lerpX = MathHelper.Lerp(prevPos.X, pos.X, _drawAlpha);
            float lerpY = MathHelper.Lerp(prevPos.Y, pos.Y, _drawAlpha);

            // ARCHITECTURE FIX: Integer-snap the camera to eliminate sub-pixel wobble
            _drawCamera.Position = new Vector2((int)lerpX, (int)lerpY);
        });

        game.GraphicsDevice.SetRenderTarget(virtualRenderTarget);
        game.GraphicsDevice.Clear(XnaColor.FromNonPremultiplied(40, 35, 50, 255));

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, camera.GetViewMatrix(VirtualWidth, VirtualHeight));

        TileRenderSystem.Draw(ecsWorld, camera, VirtualWidth, VirtualHeight);
        PlayerRenderSystem.Draw(spriteBatch, alpha);
        ProjectileRenderSystem.Draw(spriteBatch, alpha);

        spriteBatch.End();

        game.GraphicsDevice.SetRenderTarget(null);
        game.GraphicsDevice.Clear(Color.Black);

        float scaleX = (float)game.GraphicsDevice.PresentationParameters.BackBufferWidth / VirtualWidth;
        float scaleY = (float)game.GraphicsDevice.PresentationParameters.BackBufferHeight / VirtualHeight;
        float scale = Math.Min(scaleX, scaleY);

        int newW = (int)(VirtualWidth * scale);
        int newH = (int)(VirtualHeight * scale);
        var destRect = new Rectangle((game.GraphicsDevice.PresentationParameters.BackBufferWidth - newW) / 2, (game.GraphicsDevice.PresentationParameters.BackBufferHeight - newH) / 2, newW, newH);

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
        spriteBatch.Draw(virtualRenderTarget, destRect, Color.White);
        pauseMenu.Draw(spriteBatch);
        spriteBatch.End();
    }
}
