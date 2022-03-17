using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class ConfigDataBuilderSettings : ScriptableObject
    {
        private const string ConfigDataBuilderSettingsPath = "Assets/Editor/ConfigDataBuilderSettings.asset";

        [SerializeField] internal string sourceFolder = "Assets/";
        [SerializeField] internal string assemblyNamespace = "";
        [SerializeField] internal string assemblyOutputPath = "Assets/ConfigData/ConfigData.dll";
        [SerializeField] internal string l10nCustomExporterType = "";
        [SerializeField] internal string[] customTypesAssemblies = Array.Empty<string>();
        [SerializeField] internal string[] importingAssemblies = Array.Empty<string>();

        [SerializeField] internal bool publicConstructors = false;

        [SerializeField] internal DataExportType dataExportType = DataExportType.ResourcesBytesAsset;

        [SerializeField] internal string dataOutputFolder = "ConfigData/";
        
        // ResourcesBytesprivate
        //     if runtimeprivate, XConfig.Load will be internal
        [SerializeField] internal bool autoInit = true;

        internal static ConfigDataBuilderSettings TryGetSettings()
        {
            return AssetDatabase.LoadAssetAtPath<ConfigDataBuilderSettings>(ConfigDataBuilderSettingsPath);
        }

        internal static ConfigDataBuilderSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ConfigDataBuilderSettings>(ConfigDataBuilderSettingsPath);
            if (settings == null) {
                settings = CreateInstance<ConfigDataBuilderSettings>();
                if (!Directory.Exists(Path.GetDirectoryName(ConfigDataBuilderSettingsPath))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigDataBuilderSettingsPath)!);
                }
                AssetDatabase.CreateAsset(settings, ConfigDataBuilderSettingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }

    internal enum DataExportType
    {
        ResourcesBytesAsset,
        OtherBytesAsset,
    }
}
