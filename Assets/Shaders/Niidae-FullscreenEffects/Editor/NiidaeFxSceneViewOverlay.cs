using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

[Overlay(typeof(SceneView), "Niidae FX")]
public sealed class NiidaeFxSceneViewOverlay : Overlay
{
    public override VisualElement CreatePanelContent()
    {
        IMGUIContainer container = new IMGUIContainer(DrawGui);
        container.style.paddingLeft = 4;
        container.style.paddingRight = 4;
        return container;
    }

    private static void DrawGui()
    {
        List<NiidaeCustomEffectsRendererFeature> features = GetLoadedFeatures();
        bool hasFeatures = features.Count > 0;

        if (!hasFeatures)
        {
            EditorGUILayout.LabelField("Niidae FX", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("No loaded Niidae renderer feature found.", MessageType.Info);
            if (GUILayout.Button("Refresh"))
                SceneView.RepaintAll();
            return;
        }

        bool allEnabled = AreAllEnabled(features);
        bool allSame = AreAllSameEnabled(features);

        EditorGUI.showMixedValue = !allSame;
        EditorGUI.BeginChangeCheck();
        bool newState = EditorGUILayout.ToggleLeft("Niidae FX (scene view only)", allEnabled);
        EditorGUI.showMixedValue = false;

        if (EditorGUI.EndChangeCheck())
            SetEnabled(features, newState);
    }

    private static List<NiidaeCustomEffectsRendererFeature> GetLoadedFeatures()
    {
        NiidaeCustomEffectsRendererFeature[] all = Resources.FindObjectsOfTypeAll<NiidaeCustomEffectsRendererFeature>();
        List<NiidaeCustomEffectsRendererFeature> features = new List<NiidaeCustomEffectsRendererFeature>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            NiidaeCustomEffectsRendererFeature feature = all[i];
            if (feature == null)
                continue;

            if (!features.Contains(feature))
                features.Add(feature);
        }

        return features;
    }

    private static bool AreAllEnabled(List<NiidaeCustomEffectsRendererFeature> features)
    {
        for (int i = 0; i < features.Count; i++)
        {
            if (!features[i].EnableInSceneView)
                return false;
        }

        return true;
    }

    private static bool AreAllSameEnabled(List<NiidaeCustomEffectsRendererFeature> features)
    {
        if (features.Count == 0)
            return true;

        bool first = features[0].EnableInSceneView;
        for (int i = 1; i < features.Count; i++)
        {
            if (features[i].EnableInSceneView != first)
                return false;
        }

        return true;
    }

    private static void SetEnabled(List<NiidaeCustomEffectsRendererFeature> features, bool enabled)
    {
        bool changed = false;
        for (int i = 0; i < features.Count; i++)
        {
            NiidaeCustomEffectsRendererFeature feature = features[i];
            if (feature.EnableInSceneView == enabled)
                continue;

            Undo.RecordObject(feature, "Toggle Niidae FX In Scene View");
            feature.EnableInSceneView = enabled;
            EditorUtility.SetDirty(feature);
            changed = true;
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }
    }
}
