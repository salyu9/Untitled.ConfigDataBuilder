using UnityEditor;

namespace Untitled.ConfigDataBuilder.Menu
{
    public static class GuildeMenuItems
    {
        private const string NugetPackagePath =
            "Packages/com.github.salyu9.untitledconfigdatabuilder/NugetPackages/netstandard_2_0.zip";

        [MenuItem("Tools/Config Data/Locate Nuget Packages", false, 401)]
        public static void LocateNugetPackages()
        {
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(NugetPackagePath);
        }
    }
}
