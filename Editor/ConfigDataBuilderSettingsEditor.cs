using UnityEditor;
using UnityEngine;

namespace Untitled.ConfigDataBuilder.Editor
{
    [CustomEditor(typeof(ConfigDataBuilderSettings))]
    internal class ConfigDataBuilderSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            GUILayout.Space(10);
            if (GUILayout.Button("Open Config Settings Window", GUILayout.Height(30))) {
                ConfigDataBuilderSettingsProvider.Open();
            }
            GUILayout.Space(10);
        }
    }
}
