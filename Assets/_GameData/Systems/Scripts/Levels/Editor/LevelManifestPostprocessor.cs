using System;
using UnityEditor;

namespace Meowdoku
{
    /// <summary>
    /// Auto-runs <see cref="LevelManifestSync.SyncFromManifest"/> whenever the manifest itself
    /// or any level file under <see cref="LevelManifestSync.LevelsFolderPath"/> changes (added,
    /// removed, moved/renamed) - so pulling web-editor changes and letting Unity reimport is
    /// enough; there's no menu item to remember to click.
    /// </summary>
    internal sealed class LevelManifestPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool relevant = Array.IndexOf(importedAssets, LevelManifestSync.ManifestPath) >= 0
                || AnyUnderLevelsFolder(importedAssets)
                || AnyUnderLevelsFolder(deletedAssets)
                || AnyUnderLevelsFolder(movedAssets);

            if (!relevant)
            {
                return;
            }

            // Not called directly - AssetDatabase.SaveAssets() (inside SyncFromManifest) mid
            // import-batch is a known Unity footgun, so defer to the next editor tick instead.
            EditorApplication.delayCall += LevelManifestSync.SyncFromManifest;
        }

        private static bool AnyUnderLevelsFolder(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.StartsWith(LevelManifestSync.LevelsFolderPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
