using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Flecs.NET.Core;
using ImGuiNET;

using MyGame.Engine.Core;
using MyGame.Engine.Input;
using MyGame.Engine.Networking;
using MyGame.Prefabs;

using MyGame.Game.Core;
using MyGame.Game.Combat;
using MyGame.Game.Physics;
using MyGame.Game.NetworkSync;

namespace MyGame.Engine.Debug;

public class DebugUIManager
{
    private ImGuiRenderer imGuiRenderer = null!;
    private bool isVisible = true;
    private bool wasF1Pressed = false;

    private int _frameCount = 0;
    private float _fpsTimer = 0f;
    private int _currentFps = 0;

    public void Initialize(Game1 game)
    {
       imGuiRenderer = new ImGuiRenderer(game);
       imGuiRenderer.RebuildFontAtlas();
    }

    public void Draw(GameTime gameTime)
    {
       _frameCount++;
       _fpsTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
       if (_fpsTimer >= 1.0f)
       {
           _currentFps = _frameCount;
           _frameCount = 0;
           _fpsTimer -= 1.0f;
       }

       bool isF1Pressed = Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F1);
       if (isF1Pressed && !wasF1Pressed) isVisible = !isVisible;
       wasF1Pressed = isF1Pressed;

       if (!isVisible) return;

       imGuiRenderer.BeforeLayout(gameTime);

       DrawProfilerWindow();
       DrawNetworkProfiler();
       DrawLoggerConsole();
       DrawPhysicsWindow();
       DrawCombatSandbox();

       imGuiRenderer.AfterLayout();
    }

    private void DrawProfilerWindow()
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(350, 250), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Engine Profiler"))
        {
            ImGui.End();
            return;
        }

        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), $"Render FPS: {_currentFps}");
        ImGui.Text($"Logic Delta: {System.Math.Round(Game1.LastUpdateDurationMs, 2)} ms");

        float heapMb = System.GC.GetTotalMemory(false) / (1024f * 1024f);
        ImGui.Text($"Managed Heap: {System.Math.Round(heapMb, 2)} MB");
        ImGui.Text($"GC Gen 0/1/2: {System.GC.CollectionCount(0)} / {System.GC.CollectionCount(1)} / {System.GC.CollectionCount(2)}");

        ImGui.Separator();
        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), "ECS Architecture:");

        var ecs = Game1.Instance.EcsWorld;
        ImGui.Text($"Local Players: {ecs.Count<LocalPlayerTag>()}");
        ImGui.Text($"Remote Shadows: {ecs.Count<RemotePlayerTag>()}");
        ImGui.Text($"Active Projectiles: {ecs.Count<ProjectileTag>()}");
        ImGui.Text($"Entities with Health: {ecs.Count<Health>()}");

        ImGui.End();
    }

    private void DrawNetworkProfiler()
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(370, 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 200), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Network & Steamworks"))
        {
            ImGui.End();
            return;
        }

        bool steamActive = SteamManager.IsSteamActive;
        ImGui.TextColored(steamActive ? new System.Numerics.Vector4(0, 1, 0, 1) : new System.Numerics.Vector4(1, 0, 0, 1),
            steamActive ? "Steam API: ONLINE" : "Steam API: OFFLINE");

        ImGui.Separator();

        if (SteamManager.CurrentLobby is { } lobby)
        {
            bool isHost = SteamManager.KnownHostId.HasValue && SteamManager.KnownHostId.Value == Steamworks.SteamClient.SteamId;
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.8f, 0, 1), isHost ? "[ HOST ]" : "[ CLIENT ]");
            ImGui.Text($"Lobby ID: {lobby.Id}");
            ImGui.Text($"Members: {lobby.MemberCount} / {lobby.MaxMembers}");
            ImGui.Text($"State: {lobby.GetData("GameState")}");
        }
        else ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1), "Not connected to a Lobby.");

        ImGui.End();
    }

    private void DrawLoggerConsole()
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 500), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(660, 200), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Live Event Console"))
        {
            ImGui.End();
            return;
        }

        ImGui.BeginChild("LogScrollRegion", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);

        lock (EngineLogger.ConsoleLock)
        {
            foreach (var log in EngineLogger.LiveConsole)
            {
                if (log.Contains("[ERROR]")) ImGui.TextColored(new System.Numerics.Vector4(1, 0.2f, 0.2f, 1), log);
                else if (log.Contains("[NETWORK]")) ImGui.TextColored(new System.Numerics.Vector4(0.2f, 0.8f, 1, 1), log);
                else if (log.Contains("[STEAM]")) ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 1f, 1), log);
                else ImGui.TextUnformatted(log);
            }
        }

        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) ImGui.SetScrollHereY(1.0f);

        ImGui.EndChild();
        ImGui.End();
    }

    private void DrawPhysicsWindow()
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 270), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 120), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Aether2D Physics"))
        {
            ImGui.End();
            return;
        }

        var physWorld = Game1.Instance.PhysicsWorld;
        ImGui.Text($"Active Rigidbodies: {physWorld.BodyList.Count}");
        ImGui.Text($"Gravity: {physWorld.Gravity.Y}");
        ImGui.End();
    }

    private void DrawCombatSandbox()
    {
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(320, 270), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(350, 120), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Combat Sandbox"))
        {
            ImGui.End();
            return;
        }

        ImGui.Text("Local Player Testing:");
        if (ImGui.Button("Fire Projectile (Facing Dir)", new System.Numerics.Vector2(250, 30)))
        {
            var ecs = Game1.Instance.EcsWorld;
            var playerQuery = ecs.QueryBuilder<Position, FacingDirection>().With<LocalPlayerTag>().Build();

            playerQuery.Each((ref Position pos, ref FacingDirection dir) =>
            {
                ecs.Entity().Set(new ProjectileSpawnRequest
                {
                    StartX = pos.X + (dir.Value * 16f),
                    StartY = pos.Y,
                    VelocityX = dir.Value * 400f,
                    VelocityY = 0f
                });
            });
        }
        ImGui.End();
    }
}
