using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class SheetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var settings = ConfigDataBuilderSettings.TryGetSettings();
            if (settings == null) {
                return;
            }            

            var outputFolder = Path.Combine("Assets/Resources", settings.dataOutputFolder).Replace("\\", "/");

            var shouldReimportData = importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths)
                .Any(asset => asset.StartsWith(settings.sourceFolder) &&
                    !Path.GetFileName(asset).StartsWith("~") &&
                    (asset.EndsWith("xlsx", StringComparison.OrdinalIgnoreCase) || asset.EndsWith("fods", StringComparison.OrdinalIgnoreCase)));

            if (shouldReimportData) {
                ConfigDataBuilder.ReimportData();
                EditorApplication.delayCall += AssetDatabase.Refresh;
            }

            else if (Application.isPlaying && importedAssets.Any(
                asset => asset.StartsWith(outputFolder))) {
                ConfigDataBuilder.RuntimeReload();
            }
        }
    }
}
