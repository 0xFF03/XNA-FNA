using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Flecs.NET.Core;
using ImGuiNET;

using MyGame.Engine.Core;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Engine.StandardModules.Combat;
using MyGame.Game.Core;

namespace MyGame.Engine.Platform.Debug;

public class DebugUIManager
{
    private ImGuiRenderer imGuiRenderer = null!;
    private bool isVisible = true;
    private bool wasF1Pressed = false;

    private int _frameCount = 0;
    private float _fpsTimer = 0f;
    private int _currentFps = 0;

    private Query<PhysicsDimension> _dimensionQuery;
    private Query<Position, PhysicsDimension> _spaceQuery;

    public void Initialize(Game1 game)
    {
       imGuiRenderer = new ImGuiRenderer(game);
       imGuiRenderer.RebuildFontAtlas();

       _dimensionQuery = game.EcsWorld.QueryBuilder<PhysicsDimension>().With<LocalPlayerTag>().Build();
       _spaceQuery = game.EcsWorld.QueryBuilder<Position, PhysicsDimension>().With<LocalPlayerTag>().Build();
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

       ImGui.SetNextWindowPos(new System.Numerics.Vector2(20, 20), ImGuiCond.FirstUseEver);
       ImGui.SetNextWindowSize(new System.Numerics.Vector2(600, 350), ImGuiCond.FirstUseEver);

       if (ImGui.Begin("Engine Dashboard", ImGuiWindowFlags.NoCollapse))
       {
           if (ImGui.BeginTabBar("MainTabs"))
           {
               if (ImGui.BeginTabItem("Profiler")) { DrawProfilerTab(); ImGui.EndTabItem(); }
               if (ImGui.BeginTabItem("Network")) { DrawNetworkTab(); ImGui.EndTabItem(); }
               if (ImGui.BeginTabItem("Space & Vehicles")) { DrawSpaceSandboxTab(); ImGui.EndTabItem(); }
               if (ImGui.BeginTabItem("Console")) { DrawLoggerConsoleTab(); ImGui.EndTabItem(); }
               ImGui.EndTabBar();
           }
       }
       ImGui.End();

       imGuiRenderer.AfterLayout();
    }

    private void DrawProfilerTab()
    {
        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Render FPS: " + _currentFps.ToString());
        ImGui.Text("Logic Delta: " + System.Math.Round(Game1.LastUpdateDurationMs, 2).ToString() + " ms");

        float heapMb = System.GC.GetTotalMemory(false) / (1024f * 1024f);
        ImGui.Text("Managed Heap: " + System.Math.Round(heapMb, 2).ToString() + " MB");
        ImGui.Text("GC Gen 0/1/2: " + System.GC.CollectionCount(0).ToString() + " / " + System.GC.CollectionCount(1).ToString() + " / " + System.GC.CollectionCount(2).ToString());

        ImGui.Separator();
        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), "ECS Architecture:");

        var ecs = Game1.Instance.EcsWorld;
        ImGui.Text("Local Players: " + ecs.Count<LocalPlayerTag>().ToString());
        ImGui.Text("Remote Shadows: " + ecs.Count<RemotePlayerTag>().ToString());
        ImGui.Text("Active Projectiles: " + ecs.Count<BaseCombatComponents.ProjectileTag>().ToString());
        ImGui.Text("Entities with Health: " + ecs.Count<BaseCombatComponents.Health>().ToString());
    }

    private void DrawNetworkTab()
    {
        bool steamActive = SteamManager.IsSteamActive;
        ImGui.TextColored(steamActive ? new System.Numerics.Vector4(0, 1, 0, 1) : new System.Numerics.Vector4(1, 0, 0, 1),
            steamActive ? "Steam API: ONLINE" : "Steam API: OFFLINE");

        ImGui.Separator();

        if (SteamManager.CurrentLobby is { } lobby)
        {
            bool isHost = SteamManager.KnownHostId.HasValue && SteamManager.KnownHostId.Value == Steamworks.SteamClient.SteamId;
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.8f, 0, 1), isHost ? "[ HOST ]" : "[ CLIENT ]");
            ImGui.Text("Lobby ID: " + lobby.Id.Value.ToString());
            ImGui.Text("Members: " + lobby.MemberCount.ToString() + " / " + lobby.MaxMembers.ToString());
            ImGui.Text("State: " + lobby.GetData("GameState"));
        }
        else ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1), "Not connected to a Lobby.");
    }

    private void DrawSpaceSandboxTab()
    {
        string currentDim = "MacroSpace";
        string dimBuffer = currentDim;
        _dimensionQuery.Each((ref PhysicsDimension dim) => { dimBuffer = dim.Name; });
        currentDim = dimBuffer;

        var physWorld = PhysicsWorldManager.GetWorld(currentDim);
        ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), "Current Dimension: " + currentDim);
        ImGui.Text("Active Rigidbodies: " + physWorld.BodyList.Count.ToString());
        ImGui.Text("Gravity Y: " + physWorld.Gravity.Y.ToString());

        ImGui.Separator();

        _spaceQuery.Each((Entity e, ref Position pos, ref PhysicsDimension dim) =>
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 1f, 1), "Airlock Simulation:");
            if (ImGui.Button("Transfer to Space Station", new System.Numerics.Vector2(250, 30)))
            {
                e.Set(new DimensionTransferRequest { TargetDimension = "Space_Station_1", SpawnX = 100, SpawnY = 100 });
            }
            if (ImGui.Button("Transfer to MacroSpace", new System.Numerics.Vector2(250, 30)))
            {
                e.Set(new DimensionTransferRequest { TargetDimension = "MacroSpace", SpawnX = 100, SpawnY = 100 });
            }

            ImGui.Separator();
            ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 1f, 1), "Vehicle Simulation:");

            bool hasHelm = e.Has<HelmControl>();
            if (!hasHelm)
            {
                if (ImGui.Button("Take Fake Helm", new System.Numerics.Vector2(250, 30)))
                {
                    // ARCHITECTURE FIX: Use Game1.Instance.EcsWorld instead of e.World() pointer
                    Entity fakeShip = Game1.Instance.EcsWorld.Lookup("ActiveSpaceshipExterior");
                    if (fakeShip.Id == 0 || !fakeShip.IsAlive())
                    {
                        fakeShip = Game1.Instance.EcsWorld.Entity("ActiveSpaceshipExterior")
                            .Add<TopDownTag>()
                            .Set(new Position { X = pos.X, Y = pos.Y })
                            .Set(new Velocity { X = 0, Y = 0 })
                            .Set(new MovementCapabilities { MoveSpeed = 12f, JumpForce = 0 });
                    }
                    e.Set(new HelmControl { ControlledVehicle = fakeShip });
                }
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "PILOTING VEHICLE");
                if (ImGui.Button("Leave Helm", new System.Numerics.Vector2(250, 30)))
                {
                    var helm = e.Get<HelmControl>();
                    if (helm.ControlledVehicle.Id != 0 && helm.ControlledVehicle.IsAlive())
                    {
                        helm.ControlledVehicle.Destruct();
                    }
                    e.Remove<HelmControl>();
                }
            }
        });
    }

    private void DrawLoggerConsoleTab()
    {
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
    }
}
