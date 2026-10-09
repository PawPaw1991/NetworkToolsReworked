using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using NetworkToolsReworked.Tools;

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

            Settings.RegisterKeyBindings();

            // Tools only emit CreationDefinition + NetCourse entities; see Edits/NetDefinitions.cs.
            updateSystem.UpdateAt<NodeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<SlopeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<ToolActivationSystem>(SystemUpdatePhase.MainLoop);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            Settings?.UnregisterInOptionsUI();
            Settings = null;
        }
    }
}
