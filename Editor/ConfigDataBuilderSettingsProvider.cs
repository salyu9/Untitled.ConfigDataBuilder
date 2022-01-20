using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class ConfigDataBuilderSettingsProvider : SettingsProvider
    {
        private SerializedObject _settings;

        private class Styles
        {
            public static readonly GUIContent SourceFolder = new GUIContent("Source folder", "The folder where config files are in");

            public static readonly GUIContent AssemblyName = new GUIContent("Assembly name", "The name of compiled assembly");

            public static readonly GUIContent AssemblyNamespace =
                new GUIContent("Assembly namespace", "The namespace of classes in target assembly");

            public static readonly GUIContent AssemblyOutputPath = new GUIContent("Assembly output path", "The path of compiled assembly, e.g. 'Assets/ConfigData/ConfigData.dll'");

            public static readonly GUIContent DataOutput = new GUIContent("Data output",
                "Exported config data location (must be in <Resources>, for example: setting this to 'ConfigData' will make config data export to 'Assets/Resources/ConfigData/' folder)");

            public static readonly GUIContent L10nExporterType = new GUIContent("Localizations exporter type", "Exporter type to export source localization file.");

            public static readonly GUIContent CustomTypesAssemblies = new GUIContent("Custom Types Assemblies",
                "Import custom config types from these assemblies");

            public static readonly GUIContent ImportingAssemblies = new GUIContent("Importing assemblies",
                "Assemblies to import in config. Custom config data type can be imported through this way.");
        }

        public ConfigDataBuilderSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _settings = ConfigDataBuilderSettings.GetSerializedSettings();
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Separator();
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.sourceFolder)), Styles.SourceFolder);
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.assemblyName)), Styles.AssemblyName);
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.assemblyNamespace)), Styles.AssemblyNamespace);
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.assemblyOutputPath)), Styles.AssemblyOutputPath);
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.dataOutputFolder)), Styles.DataOutput);
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.l10nCustomExporterType)), Styles.L10nExporterType);
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.customTypesAssemblies)), Styles.CustomTypesAssemblies);
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.importingAssemblies)), Styles.ImportingAssemblies);
            
            _settings.ApplyModifiedPropertiesWithoutUndo();
        }

        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            return new ConfigDataBuilderSettingsProvider("Project/Config Data Builder", SettingsScope.Project) {
                // Automatically extract all keywords from the Styles.
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };
        }

        public static void Open()
        {
            SettingsService.OpenProjectSettings("Project/Config Data Builder");
        }
    }
}
