using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class ConfigDataBuilderSettings : ScriptableObject
    {
        internal const string ConfigDataBuilderSettingsPath = "Assets/Editor/ConfigDataBuilderSettings.asset";

        [SerializeField] internal string sourceFolder = "Assets/";
        [SerializeField] internal string assemblyName = "ConfigData";
        [SerializeField] internal string assemblyNamespace = "";
        [SerializeField] internal string assemblyOutputPath = "Assets/ConfigData/ConfigData.dll";
        [SerializeField] internal string dataOutputFolder = "Assets/Resources/ConfigData/";
        [SerializeField] internal string l10nCustomExporterType = "";
        [SerializeField] internal string[] customTypesAssemblies = Array.Empty<string>();
        [SerializeField] internal string[] importingAssemblies = Array.Empty<string>();

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
}
