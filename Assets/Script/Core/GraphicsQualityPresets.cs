using UnityEngine;

/// <summary>PC presets share the same renderer and art direction, with scalable budgets.</summary>
public static class GraphicsQualityPresets
{
    public readonly struct Profile
    {
        public readonly string Name;
        public readonly int InternalHeight, BloomIterations, MaxFullLodTrees;
        public readonly float ShadowDistance, TreeDistance, BillboardDistance, TreeFade,
            DetailDistance, DetailDensity, LightDistance, LocalShadowDistance, LodBias;
        public readonly bool PointLightShadows;

        public Profile(string name, int height, int bloom, float shadows, float trees,
            float billboard, float fade, int fullLod, float details, float density,
            float lights, float localShadows, float lodBias, bool pointShadows = false)
        {
            Name = name; InternalHeight = height; BloomIterations = bloom;
            ShadowDistance = shadows; TreeDistance = trees; BillboardDistance = billboard;
            TreeFade = fade; MaxFullLodTrees = fullLod; DetailDistance = details;
            DetailDensity = density; LightDistance = lights; LocalShadowDistance = localShadows;
            LodBias = lodBias; PointLightShadows = pointShadows;
        }
    }

    private static readonly Profile[] Profiles =
    {
        new Profile("Very Low", 720, 0, 25, 350, 75, 25, 128, 25, 0.3f, 22, 0, 0.7f),
        new Profile("Low", 900, 1, 35, 450, 100, 35, 256, 38, 0.45f, 28, 10, 1f),
        new Profile("Medium", 1080, 2, 45, 550, 130, 45, 512, 55, 0.7f, 35, 18, 1.3f),
        new Profile("High", 1440, 4, 60, 650, 160, 65, 1024, 70, 0.85f, 45, 28, 1.7f),
        new Profile("Ultra", 0, 8, 75.61f, 650, 190, 120, 2048, 82, 1f, 60, 45, 2f, true)
    };

    public static Profile Current => Get(GameSettings.CurrentGraphicsQuality);
    public static Profile Get(GameSettings.GraphicsQuality quality) => Profiles[Mathf.Clamp((int)quality, 0, Profiles.Length - 1)];

    public static float SuggestedRenderScale(GameSettings.GraphicsQuality quality, int outputHeight)
    {
        int height = Get(quality).InternalHeight;
        return height == 0 ? 1f : Mathf.Clamp(height / (float)Mathf.Max(1, outputHeight), 0.25f, 1f);
    }
}
