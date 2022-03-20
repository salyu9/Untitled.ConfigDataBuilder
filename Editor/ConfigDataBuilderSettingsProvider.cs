using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace Untitled.ConfigDataBuilder.Editor
{
    internal class ConfigDataBuilderSettingsProvider : SettingsProvider
    {
        private SerializedObject _settings;

        private class Styles
        {
            public static readonly GUIContent SourceFolder = new GUIContent("Source Folder",
                "The folder where config files are in.");

            public static readonly GUIContent AssemblyNamespace = new GUIContent("Assembly Namespace",
                "The namespace of classes in target assembly.");

            public static readonly GUIContent AssemblyOutputPath = new GUIContent("Assembly Output Path",
                "The path of compiled assembly, e.g. 'Assets/ConfigData/ConfigData.dll'.");

            public static readonly GUIContent PublicConstructors = new GUIContent("Public Constructors",
                "Generate public constructors for config classes. If not enabled, your script will not able to create instances of config classes");

            public static readonly GUIContent FlagRowCount = new GUIContent("Flag Row Count");

            public static readonly GUIContent DataExportType = new GUIContent("Data Export Type",
                "Control how the config data are exported.\n" + 
                "  ResourcesBytesAsset: data will be exported to *.bytes in resources folder, can be auto-init at runtime.\n" + 
                "  OtherBytesAsset: data will be exported to *.bytes at other specified location. You will need to load the asset by yourself (i.e. using bundles or addressables)");

            public static readonly GUIContent AutoInit = new GUIContent("Auto Init",
                "Auto load config data from resources when config data is queried (AllConfg / From<X> is called)");
                
            public static readonly GUIContent DataOutput = new GUIContent("Data Output",
                "Exported config data location (must be in <Resources>, for example: setting this to 'ConfigData' will make config data export to 'Assets/Resources/ConfigData/' folder).");

            public static readonly GUIContent L10nExporterType = new GUIContent("Localizations Exporter Type",
                "Exporter type to export source localization file.");

            public static readonly GUIContent CustomTypesAssemblies = new GUIContent("Custom Types Assemblies",
                "Import custom config types from these assemblies.");

            public static readonly GUIContent ImportingAssemblies = new GUIContent("Importing Assemblies",
                "Assemblies to import in config. Custom config data type can be imported through this way.");
        }

        public ConfigDataBuilderSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        { }

        private static ReorderableList GetReorderableList(SerializedObject @object, string propertyName)
        {
            var property = @object.FindProperty(propertyName);
            return new ReorderableList(@object, property, draggable: true, displayHeader: false, displayAddButton: true, displayRemoveButton: true) {
                drawElementCallback = (rect, index, active, focused) => {
                    var elem = property.GetArrayElementAtIndex(index);
                    EditorGUI.PropertyField(rect, elem, GUIContent.none);
                },
                elementHeightCallback = index => {
                    if (index < 0 || index >= property.arraySize) {
                        return 0;
                    }
                    return EditorGUI.GetPropertyHeight(property.GetArrayElementAtIndex(index));
                }
            };
        }

        private ReorderableList _customTypesAssembliesList;

        private ReorderableList _importingAssembliesList;

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _settings = ConfigDataBuilderSettings.GetSerializedSettings();
            _customTypesAssembliesList = GetReorderableList(_settings, nameof(ConfigDataBuilderSettings.customTypesAssemblies));
            _importingAssembliesList = GetReorderableList(_settings, nameof(ConfigDataBuilderSettings.importingAssemblies));
        }

        public override void OnDeactivate()
        {
            _customTypesAssembliesList = null;
            _importingAssembliesList = null;
        }

        public override void OnGUI(string searchContext)
        {
            const int labelWidth = 200;

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.SourceFolder, GUILayout.Width(labelWidth));
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.sourceFolder)), GUIContent.none);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.AssemblyNamespace, GUILayout.Width(labelWidth));
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.assemblyNamespace)), GUIContent.none);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.AssemblyOutputPath, GUILayout.Width(labelWidth));
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.assemblyOutputPath)), GUIContent.none);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.PublicConstructors, GUILayout.Width(labelWidth));
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.publicConstructors)), GUIContent.none);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.FlagRowCount, GUILayout.Width(labelWidth));
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.flagRowCount)), GUIContent.none);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var exportTypeProperty = _settings.FindProperty(nameof(ConfigDataBuilderSettings.dataExportType));
            EditorGUILayout.LabelField(Styles.DataExportType, GUILayout.Width(labelWidth));
            EditorGUILayout.PropertyField(exportTypeProperty, GUIContent.none);
            EditorGUILayout.EndHorizontal();

            var exportType = (DataExportType)exportTypeProperty.enumValueIndex;

            if (exportType == DataExportType.ResourcesBytesAsset) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Styles.DataOutput, GUILayout.Width(labelWidth));
                EditorGUILayout.LabelField("Assets/Resources/", GUILayout.Width(110));
                EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.dataOutputFolder)), GUIContent.none);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Styles.AutoInit, GUILayout.Width(labelWidth));
                EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.autoInit)), GUIContent.none);
                EditorGUILayout.EndHorizontal();
            }
            else if (exportType == DataExportType.OtherBytesAsset) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Styles.DataOutput, GUILayout.Width(labelWidth));
                EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.dataOutputFolder)), GUIContent.none);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.L10nExporterType, GUILayout.Width(labelWidth));
            EditorGUILayout.PropertyField(_settings.FindProperty(nameof(ConfigDataBuilderSettings.l10nCustomExporterType)), GUIContent.none);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.CustomTypesAssemblies, GUILayout.Width(labelWidth));
            _customTypesAssembliesList.DoLayoutList();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.ImportingAssemblies, GUILayout.Width(labelWidth));
            _importingAssembliesList.DoLayoutList();
            EditorGUILayout.EndHorizontal();

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
