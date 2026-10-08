using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace NetworkToolsReworked
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger($"{nameof(NetworkToolsReworked)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public static Setting Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Log.Info($"Current mod asset at {asset.path}");

            Settings = new Setting(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            AssetDatabase.global.LoadSettings(nameof(NetworkToolsReworked), Settings, new Setting(this));

            // Tool systems are registered here as they are implemented. Each one runs in
            // SystemUpdatePhase.ToolUpdate and only emits CreationDefinition + NetCourse entities.
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            Settings?.UnregisterInOptionsUI();
            Settings = null;
        }
    }
}
