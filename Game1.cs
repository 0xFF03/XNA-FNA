using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyGame.Engine.States;
using MyGame.GameStates;
using MyGame.Engine.Networking;

namespace MyGame;

public class Game1 : Game
{
	private readonly GraphicsDeviceManager graphics;
	private SpriteBatch? spriteBatch;
	private readonly StateManager stateManager;

	// Singleton exposure for cross-assembly or network-driven state modifications
	public static Game1 Instance { get; private set; } = null!;

	public Game1()
	{
		Instance = this;

		graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 1280,
			PreferredBackBufferHeight = 720
		};
		Content.RootDirectory = "Content";
		IsMouseVisible = true;

		stateManager = new StateManager();
	}

	protected override void Initialize()
	{
		// SteamManager.Initialize() is handled via Program.cs before Vulkan surface creation
		base.Initialize();
	}

	protected override void LoadContent()
	{
		spriteBatch = new SpriteBatch(GraphicsDevice);
		stateManager.ChangeState(new MainMenuState(this, stateManager));
	}

	protected override void Update(GameTime gameTime)
	{
		if (Keyboard.GetState().IsKeyDown(Keys.Escape))
			Exit();

		SteamManager.Update();
		stateManager.Update(gameTime);

		base.Update(gameTime);
	}

	protected override void Draw(GameTime gameTime)
	{
		spriteBatch?.Begin();
		stateManager.Draw(spriteBatch!);
		spriteBatch?.End();

		base.Draw(gameTime);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			spriteBatch?.Dispose();
			SteamManager.Shutdown();
		}
		base.Dispose(disposing);
	}
}
