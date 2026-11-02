// Object-space crown shading shared by the live leaves and Terrain atlas bake.
// Evaluated before wind / billboard deformation so shading stays attached to the tree.
half3 GetTreeCrownTint(float3 positionOS)
{
    float height01 = saturate((positionOS.y - _CrownShape.y) * _CrownShape.z);
    float radius = max(0.01, _CrownShape.x *
        lerp(1.0, _CrownShape.w, smoothstep(0.25, 1.0, height01)));
    float radial01 = length(positionOS.xz) / radius;
    half interior = 1.0h - smoothstep(0.12, 1.0, radial01);
    half3 interiorTint = _CrownInnerTint.rgb * (1.0h - _CrownShadeStrength);
    return lerp(half3(1.0h, 1.0h, 1.0h), interiorTint, interior);
}

// Desaturate albedo before lighting, consistently in meshes and the Terrain atlas.
half3 MuteTreeLeafColor(half3 albedo)
{
    half luminance = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));
    return lerp(luminance.xxx, albedo, saturate(_LeafSaturation));
}
