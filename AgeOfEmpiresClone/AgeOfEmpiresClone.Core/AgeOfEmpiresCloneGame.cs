using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AgeOfEmpiresClone.Core.Effects;
using AgeOfEmpiresClone.Core.Localization;
using AgeOfEmpiresClone.Core.Screens;
using AgeOfEmpiresClone.Core.Settings;
using AgeOfEmpiresClone.ScreenManagers;
using AgeOfEmpiresClone.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AgeOfEmpiresClone.Core
{
    /// <summary>
    /// The main class for the game, responsible for managing game components, settings, 
    /// and platform-specific configurations.
    /// </summary>
    /// <remarks>
    /// This class is the entry point for the game and handles initialization, content loading,
    /// and screen management.
    /// </remarks>}
    public class AgeOfEmpiresCloneGame : Game
    {
        // Resources for drawing.
        private GraphicsDeviceManager graphicsDeviceManager;

        // ApplyChanges() loest selbst wieder ClientSizeChanged aus — ohne
        // diese Sperre ruft sich die Behandlung endlos auf.
        private bool handlingResize;

        // Manages the game's screen transitions and screens.
        private ScreenManager screenManager;

        // Manages game settings, such as preferences and configurations.
        private SettingsManager<AgeOfEmpiresCloneSettings> settingsManager;

        // Manages leaderboard data for tracking high scores and achievements.
        private SettingsManager<AgeOfEmpiresCloneLeaderboard> leaderboardManager;

        // Texture for rendering particles.
        private Texture2D particleTexture;

        // Manages particle effects in the game.
        private ParticleManager particleManager;

        /// <summary>
        /// Indicates if the game is running on a mobile platform.
        /// </summary>
        public readonly static bool IsMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        /// <summary>
        /// Indicates if the game is running on a desktop platform.
        /// </summary>
        public readonly static bool IsDesktop = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

        /// <summary>
        /// Initializes a new instance of the game. Configures platform-specific settings, 
        /// initializes services like settings and leaderboard managers, and sets up the 
        /// screen manager for screen transitions.
        /// </summary>
        public AgeOfEmpiresCloneGame()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);

            // Share GraphicsDeviceManager as a service.
            Services.AddService(typeof(GraphicsDeviceManager), graphicsDeviceManager);

            // Determine the appropriate settings storage based on the platform.
            ISettingsStorage storage;
            if (IsMobile)
            {
                storage = new MobileSettingsStorage();
                graphicsDeviceManager.IsFullScreen = true;
                IsMouseVisible = false;
            }
            else if (IsDesktop)
            {
                storage = new DesktopSettingsStorage();
                graphicsDeviceManager.IsFullScreen = false;
                // Fenstergröße am Bildschirm ausrichten. Fest verdrahtete
                // 1280×768 waren auf hochauflösenden Anzeigen winzig.
                var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                graphicsDeviceManager.PreferredBackBufferWidth =
                    Math.Max(1280, (int)(display.Width * 0.8f));
                graphicsDeviceManager.PreferredBackBufferHeight =
                    Math.Max(768, (int)(display.Height * 0.8f));

                Window.AllowUserResizing = true;
                Window.ClientSizeChanged += OnClientSizeChanged;
                IsMouseVisible = true;
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            // Initialize settings and leaderboard managers.
            settingsManager = new SettingsManager<AgeOfEmpiresCloneSettings>(storage);
            Services.AddService(typeof(SettingsManager<AgeOfEmpiresCloneSettings>), settingsManager);

            leaderboardManager = new SettingsManager<AgeOfEmpiresCloneLeaderboard>(storage);
            Services.AddService(typeof(SettingsManager<AgeOfEmpiresCloneLeaderboard>), leaderboardManager);

            Content.RootDirectory = "Content";

            // Configure screen orientations.
            graphicsDeviceManager.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;

            // Initialize the screen manager.
            screenManager = new ScreenManager(this);
            Components.Add(screenManager);
        }

        /// <summary>
        /// Übernimmt eine vom Benutzer geänderte Fenstergröße in den
        /// Bildpuffer. Ohne das bliebe der Puffer auf der Startgröße und das
        /// Bild würde verzerrt skaliert.
        /// </summary>
        private void OnClientSizeChanged(object sender, EventArgs e)
        {
            if (handlingResize)
                return;

            var bounds = Window.ClientBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            handlingResize = true;
            try
            {
                graphicsDeviceManager.PreferredBackBufferWidth = bounds.Width;
                graphicsDeviceManager.PreferredBackBufferHeight = bounds.Height;
                graphicsDeviceManager.ApplyChanges();
            }
            finally
            {
                handlingResize = false;
            }
        }

        /// <summary>
        /// Initializes the game, including setting up localization and adding the 
        /// initial screens to the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            // Load supported languages and set the default language.
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();
            var languages = new List<CultureInfo>();
            for (int i = 0; i < cultures.Count; i++)
            {
                languages.Add(cultures[i]);
            }
            var selectedLanguage = languages[settingsManager.Settings.Language].Name;
            LocalizationManager.SetCulture(selectedLanguage);

            // Boot into the menu. The RTS mode is reachable from there, or directly
            // via the --rts command line switch (see below).
            if (Environment.GetCommandLineArgs().Contains("--rts"))
            {
                screenManager.AddScreen(new RTSGameplayScreen(), null);
            }
            else
            {
                screenManager.AddScreen(new BackgroundScreen(), null);
                screenManager.AddScreen(new MainMenuScreen(), null);
            }
        }

        /// <summary>
        /// Loads game content, such as textures and particle systems.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            // Load a texture for particles and initialize the particle manager.
            particleTexture = Content.Load<Texture2D>("Sprites/blank");
            particleManager = new ParticleManager(particleTexture, new Vector2(400, 200));

            // Share the particle manager as a service.
            Services.AddService(typeof(ParticleManager), particleManager);
        }
    }
}
