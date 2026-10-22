using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LightsPowerGrid))]
public class LightsPowerGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LightsPowerGrid powerGrid = (LightsPowerGrid)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Test Controls", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("ON"))
            {
                powerGrid.PowerOn();
            }

            if (GUILayout.Button("OFF"))
            {
                powerGrid.PowerOff();
            }

            if (GUILayout.Button("TOGGLE"))
            {
                powerGrid.TogglePower();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Test buttons are available in Play Mode.", MessageType.Info);
        }
    }
}
