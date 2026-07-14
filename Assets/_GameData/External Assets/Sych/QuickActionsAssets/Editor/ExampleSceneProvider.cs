using System.IO;
using Sych.QuickActionsAssets.Editor.Tools;

namespace Sych.QuickActionsAssets.Editor
{
    internal sealed class ExampleSceneProvider
    {
        public static string GetScenePath()
        {
            try
            {
                var scriptPath = ScriptPathProvider.GetScriptPath(nameof(ExampleSceneProvider));
                if (string.IsNullOrEmpty(scriptPath))
                    return null;

                var scriptDirectoryPath = Path.GetDirectoryName(scriptPath);
                if (scriptDirectoryPath == null)
                    return null;

                var assetsDirectoryPath = Path.GetDirectoryName(scriptDirectoryPath);
                if (assetsDirectoryPath == null)
                    return null;

                var scenePath = Path.Combine(assetsDirectoryPath, "Example", "Scenes", "Example.unity");
                return File.Exists(scenePath) ? scenePath : null;
            }
            catch
            {
                return null;
            }
        }
    }
}