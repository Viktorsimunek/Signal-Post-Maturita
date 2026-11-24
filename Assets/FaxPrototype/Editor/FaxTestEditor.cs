using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FaxTest))]
public class FaxTestEditor : Editor
{
    private bool advanced;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("font"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("inkEffect"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("renderScale"), new GUIContent("Rozliseni (1x / 2x)"));
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("documentJson"));
        bool changedDocument = EditorGUI.EndChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useQuality"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("quality"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("CH_Logo"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chLogo"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("logoSize"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("logoInset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("seed"));
        EditorGUILayout.HelpBox("1 = cisty prijem, 0 = silne poskozeny fax. JSON se nacita pri spusteni nebo tlacitkem. Kvalitu pak muzes ladit rucne.", MessageType.Info);
        advanced = EditorGUILayout.Foldout(advanced, "Pokrocile nastaveni a rucni text", true);
        if (advanced)
            DrawPropertiesExcluding(serializedObject, "m_Script", "font", "inkEffect", "documentJson",
                "useQuality", "quality", "CH_Logo", "chLogo", "logoSize", "logoInset", "seed", "renderScale");
        serializedObject.ApplyModifiedProperties();
        if (changedDocument || GUILayout.Button("Nacist JSON"))
        {
            Undo.RecordObject(target, "Nacist fax JSON");
            ((FaxTest)target).LoadDocument();
            EditorUtility.SetDirty(target);
        }
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
            if (GUILayout.Button("Vygenerovat nahled"))
                ((FaxTest)target).Regenerate();
        var archive = ((FaxTest)target).GetComponent<FaxArchiveTest>();
        if (archive != null)
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying || archive.IsSaving))
                if (GUILayout.Button("Ulozit test 4 barev PNG")) archive.SaveTestFax();
            EditorGUILayout.HelpBox(archive.LastSummary, MessageType.Info);
        }
    }
}
