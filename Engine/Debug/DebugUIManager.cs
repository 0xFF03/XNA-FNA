#if DEBUG
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Flecs.NET.Core;
using ImGuiNET;

using MyGame.Engine.Core;
using MyGame.Engine.Platform.Networking;
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

    private World _ecsWorld;
    private Query<PhysicsDimension> _dimensionQuery;
    private Query<Position, PhysicsDimension> _spaceQuery;
    private Query<ShipVehicleComponent> _shipQuery;

    private string _targetIp = "127.0.0.1:7777";

    public void Initialize(Game1 game)
    {
        imGuiRenderer = new ImGuiRenderer(game);
        imGuiRenderer.RebuildFontAtlas();

        _ecsWorld = game.EcsWorld;

        _dimensionQuery = _ecsWorld.QueryBuilder<PhysicsDimension>().With<LocalPlayerTag>().Build();
        _spaceQuery = _ecsWorld.QueryBuilder<Position, PhysicsDimension>().With<LocalPlayerTag>().Build();
        _shipQuery = _ecsWorld.QueryBuilder<ShipVehicleComponent>().Build();
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

        ImGui.Text("Local Players: " + _ecsWorld.Count<LocalPlayerTag>().ToString());
        ImGui.Text("Remote Shadows: " + _ecsWorld.Count<RemotePlayerTag>().ToString());
        ImGui.Text("Active Projectiles: " + _ecsWorld.Count<BaseCombatComponents.ProjectileTag>().ToString());
        ImGui.Text("Entities with Health: " + _ecsWorld.Count<BaseCombatComponents.Health>().ToString());
    }

    private void DrawNetworkTab()
    {
        var netProvider = NetworkServiceLocator.Provider;
        bool netActive = netProvider.IsActive;

        ImGui.TextColored(netActive ? new System.Numerics.Vector4(0, 1, 0, 1) : new System.Numerics.Vector4(1, 0, 0, 1),
            netActive ? $"Network Service: {netProvider.GetType().Name} (ONLINE)" : "Network Service: OFFLINE");

        ImGui.Separator();
        ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 1f, 1), "Active Transport Layer:");

        if (ImGui.Button("Steam API", new System.Numerics.Vector2(120, 25)))
            NetworkServiceLocator.SwitchProvider(NetworkProviderType.Steam);

        ImGui.SameLine();

        if (ImGui.Button("Local UDP", new System.Numerics.Vector2(120, 25)))
            NetworkServiceLocator.SwitchProvider(NetworkProviderType.LocalUdp);

        ImGui.Separator();

        if (netProvider is UdpNetworkService)
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), "Local Multi-Instance Testing");
            ImGui.InputText("Target IP:Port", ref _targetIp, 32);
            if (ImGui.Button("Join Target IP", new System.Numerics.Vector2(150, 25)))
            {
                netProvider.JoinLobby(_targetIp);
                MyGame.Engine.Platform.StateManager.Instance.ChangeState(new MyGame.Game.UIStates.CharacterSelectState(Game1.Instance, MyGame.Engine.Platform.StateManager.Instance));
            }
            ImGui.Separator();

            ImGui.TextColored(new System.Numerics.Vector4(1, 0.5f, 0, 1), "Network Degradation Simulator");
            ImGui.SliderFloat("Packet Loss %", ref UdpNetworkService.SimulatedPacketLossPercent, 0.0f, 1.0f);
            ImGui.SliderInt("Base Ping (ms)", ref UdpNetworkService.SimulatedLatencyMs, 0, 1000);
            ImGui.SliderInt("Jitter Variance (ms)", ref UdpNetworkService.SimulatedJitterMs, 0, 500);
            ImGui.Separator();
        }

        if (netProvider.IsInLobby)
        {
            bool isHost = netProvider.HostId.HasValue && netProvider.HostId.Value == netProvider.LocalUserId;
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.8f, 0, 1), isHost ? "[ HOST ]" : "[ CLIENT ]");
            ImGui.Text($"Local ID: {netProvider.LocalUserId}");
            ImGui.Text($"Connected Peers: {netProvider.ActivePeers.Count}");
        }
        else
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1), "Not connected to a session.");
        }
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

            if (ImGui.Button("Transfer to Spaceship Interior", new System.Numerics.Vector2(250, 30)))
            {
                string targetDim = "Ship_Interior";
                _shipQuery.Each((ref ShipVehicleComponent s) => { targetDim = s.InteriorDimensionName; });

                e.Set(new DimensionTransferRequest { TargetDimension = targetDim, SnapToInteriorAirlock = true });
            }
            if (ImGui.Button("Transfer to MacroSpace", new System.Numerics.Vector2(250, 30)))
            {
                string targetDim = "MacroSpace";
                var mapData = LevelManager.GetCachedLevel(targetDim);
                float tx = mapData?.SpawnPoint.X ?? 100f;
                float ty = mapData?.SpawnPoint.Y ?? 100f;

                e.Set(new DimensionTransferRequest { TargetDimension = targetDim, ExplicitSpawnX = tx, ExplicitSpawnY = ty });
            }

            ImGui.Separator();
            ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 1f, 1), "Vehicle Simulation:");

            bool hasHelm = e.Has<HelmControl>();
            if (!hasHelm)
            {
                if (ImGui.Button("Take Fake Helm", new System.Numerics.Vector2(250, 30)))
                {
                    Entity fakeShip = _ecsWorld.Lookup("ActiveSpaceshipExterior");
                    if (fakeShip.Id == 0 || !fakeShip.IsAlive())
                    {
                        fakeShip = _ecsWorld.Entity("ActiveSpaceshipExterior")
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
                if (log.Contains("[ERROR]") || log.Contains("[FATAL]")) ImGui.TextColored(new System.Numerics.Vector4(1, 0.2f, 0.2f, 1), log);
                else if (log.Contains("[NETWORK]")) ImGui.TextColored(new System.Numerics.Vector4(0.2f, 0.8f, 1, 1), log);
                else if (log.Contains("[STEAM]")) ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 1f, 1), log);
                else ImGui.TextUnformatted(log);
            }
        }

        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) ImGui.SetScrollHereY(1.0f);

        ImGui.EndChild();
    }
}
#endif
