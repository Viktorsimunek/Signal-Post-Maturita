using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(FirstPersonController))]
public class PlayerFootstepAudio : MonoBehaviour
{
    [System.Serializable]
    private struct MaterialFootstepSet
    {
        public Material material;
        public AudioClip[] clips;
        [Min(0f)] public float volume;
    }

    [System.Serializable]
    private struct PhysicsMaterialFootstepSet
    {
        public PhysicsMaterial physicsMaterial;
        public AudioClip[] clips;
        [Min(0f)] public float volume;
    }

    [System.Serializable]
    private struct TerrainLayerFootstepSet
    {
        public TerrainLayer terrainLayer;
        public AudioClip[] clips;
        [Min(0f)] public float volume;
    }

    [Header("References")]
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private AudioSource footstepAudioSource;

    [Header("Surface Audio")]
    [SerializeField] private AudioClip[] defaultFootstepClips;
    [SerializeField] private MaterialFootstepSet[] materialFootsteps;
    [SerializeField] private PhysicsMaterialFootstepSet[] physicsMaterialFootsteps;
    [SerializeField] private TerrainLayerFootstepSet[] terrainLayerFootsteps;

    [Header("Stepping")]
    [SerializeField] private LayerMask footstepSurfaceMask = ~0;
    [SerializeField] private float minFootstepSpeed = 0.45f;
    [SerializeField] private float walkStepDistance = 1.85f;
    [SerializeField] private float briskStepDistance = 1.45f;
    [SerializeField] private float footstepProbeStart = 0.42f;
    [SerializeField] private float footstepProbeDistance = 1f;
    [SerializeField] private float footstepProbeRadius = 0.18f;

    [Header("Playback")]
    [SerializeField] private float footstepVolume = 1f;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.96f, 1.04f);
    [SerializeField] private Vector2 footstepVolumeRange = new Vector2(0.92f, 1f);
    [SerializeField, Range(0f, 1f)] private float footstepSpatialBlend;

    private float footstepDistanceProgress;
    private RaycastHit cachedGroundHit;
    private bool hasCachedGroundHit;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<FirstPersonController>();
        }

        EnsureFootstepAudioSource();
    }

    private void OnValidate()
    {
        footstepPitchRange = SortVector(footstepPitchRange);
        footstepVolumeRange = SortVector(footstepVolumeRange);

        minFootstepSpeed = Mathf.Max(0f, minFootstepSpeed);
        walkStepDistance = Mathf.Max(0.1f, walkStepDistance);
        briskStepDistance = Mathf.Max(0.1f, briskStepDistance);
        footstepProbeStart = Mathf.Max(0.05f, footstepProbeStart);
        footstepProbeDistance = Mathf.Max(0.1f, footstepProbeDistance);
        footstepProbeRadius = Mathf.Max(0.01f, footstepProbeRadius);
        footstepVolume = Mathf.Max(0f, footstepVolume);

        if (controller == null)
        {
            controller = GetComponent<FirstPersonController>();
        }

        if (footstepAudioSource != null)
        {
            ConfigureFootstepAudioSource(footstepAudioSource);
        }
    }

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        UpdateFootsteps();
    }

    private void UpdateFootsteps()
    {
        float planarSpeed = controller.PlanarSpeed;
        hasCachedGroundHit = TryGetGroundHit(out cachedGroundHit);

        if (!controller.IsGrounded || planarSpeed < minFootstepSpeed)
        {
            footstepDistanceProgress = 0f;
            return;
        }

        footstepDistanceProgress += planarSpeed * Time.deltaTime;

        float currentStepDistance = GetCurrentStepDistance(planarSpeed);
        while (footstepDistanceProgress >= currentStepDistance)
        {
            footstepDistanceProgress -= currentStepDistance;
            PlayFootstep();
            currentStepDistance = GetCurrentStepDistance(planarSpeed);
        }
    }

    private float GetCurrentStepDistance(float planarSpeed)
    {
        if (controller.BriskSpeed <= controller.WalkSpeed + 0.001f)
        {
            return walkStepDistance;
        }

        float speedT = Mathf.InverseLerp(controller.WalkSpeed, controller.BriskSpeed, planarSpeed);
        return Mathf.Lerp(walkStepDistance, briskStepDistance, speedT);
    }

    private void PlayFootstep()
    {
        if (footstepAudioSource == null)
        {
            return;
        }

        if (!TryResolveFootstep(out AudioClip clip, out float volumeScale) || clip == null)
        {
            return;
        }

        footstepAudioSource.pitch = RandomRange(footstepPitchRange);
        float randomizedVolume = footstepVolume * volumeScale * RandomRange(footstepVolumeRange);
        footstepAudioSource.PlayOneShot(clip, randomizedVolume);
    }

    private bool TryResolveFootstep(out AudioClip clip, out float volumeScale)
    {
        volumeScale = 1f;
        clip = null;

        if (hasCachedGroundHit)
        {
            if (TryGetTerrainFootstepSet(cachedGroundHit, out TerrainLayerFootstepSet terrainSet))
            {
                clip = GetRandomClip(terrainSet.clips);
                if (clip != null)
                {
                    volumeScale = ResolveVolumeScale(terrainSet.volume);
                    return true;
                }
            }

            if (TryGetSurfaceMaterial(cachedGroundHit, out Material surfaceMaterial) &&
                TryGetMaterialFootstepSet(surfaceMaterial, out MaterialFootstepSet materialSet))
            {
                clip = GetRandomClip(materialSet.clips);
                if (clip != null)
                {
                    volumeScale = ResolveVolumeScale(materialSet.volume);
                    return true;
                }
            }

            PhysicsMaterial sharedPhysicsMaterial = cachedGroundHit.collider != null
                ? cachedGroundHit.collider.sharedMaterial
                : null;

            if (sharedPhysicsMaterial != null &&
                TryGetPhysicsMaterialFootstepSet(sharedPhysicsMaterial, out PhysicsMaterialFootstepSet physicsSet))
            {
                clip = GetRandomClip(physicsSet.clips);
                if (clip != null)
                {
                    volumeScale = ResolveVolumeScale(physicsSet.volume);
                    return true;
                }
            }
        }

        clip = GetRandomClip(defaultFootstepClips);
        return clip != null;
    }

    private bool TryGetGroundHit(out RaycastHit hit)
    {
        Vector3 origin = transform.position + (Vector3.up * footstepProbeStart);
        float probeDistance = footstepProbeStart + footstepProbeDistance;
        CharacterController characterController = controller.Controller;

        if (Physics.SphereCast(
                origin,
                footstepProbeRadius,
                Vector3.down,
                out hit,
                probeDistance,
                footstepSurfaceMask,
                QueryTriggerInteraction.Ignore) &&
            hit.collider != characterController)
        {
            return true;
        }

        if (Physics.Raycast(
                origin,
                Vector3.down,
                out hit,
                probeDistance,
                footstepSurfaceMask,
                QueryTriggerInteraction.Ignore) &&
            hit.collider != characterController)
        {
            return true;
        }

        return false;
    }

    private bool TryGetTerrainFootstepSet(RaycastHit groundHit, out TerrainLayerFootstepSet footstepSet)
    {
        footstepSet = default;

        Terrain terrain = groundHit.collider != null
            ? groundHit.collider.GetComponent<Terrain>() ?? groundHit.collider.GetComponentInParent<Terrain>()
            : null;

        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        TerrainLayer[] terrainLayers = terrainData.terrainLayers;
        if (terrainLayers == null || terrainLayers.Length == 0)
        {
            return false;
        }

        Vector3 localPosition = groundHit.point - terrain.transform.position;
        if (terrainData.size.x <= 0.0001f || terrainData.size.z <= 0.0001f)
        {
            return false;
        }

        int alphaX = Mathf.Clamp(
            Mathf.RoundToInt((localPosition.x / terrainData.size.x) * (terrainData.alphamapWidth - 1)),
            0,
            terrainData.alphamapWidth - 1);
        int alphaZ = Mathf.Clamp(
            Mathf.RoundToInt((localPosition.z / terrainData.size.z) * (terrainData.alphamapHeight - 1)),
            0,
            terrainData.alphamapHeight - 1);

        float[,,] weights = terrainData.GetAlphamaps(alphaX, alphaZ, 1, 1);
        float bestWeight = -1f;

        for (int i = 0; i < terrainLayers.Length && i < weights.GetLength(2); i++)
        {
            TerrainLayer layer = terrainLayers[i];
            if (layer == null || !TryGetTerrainLayerFootstepSet(layer, out TerrainLayerFootstepSet candidate))
            {
                continue;
            }

            float weight = weights[0, 0, i];
            if (weight <= bestWeight)
            {
                continue;
            }

            bestWeight = weight;
            footstepSet = candidate;
        }

        return bestWeight >= 0f;
    }

    private bool TryGetSurfaceMaterial(RaycastHit groundHit, out Material material)
    {
        material = null;

        if (groundHit.collider == null)
        {
            return false;
        }

        Renderer renderer = groundHit.collider.GetComponent<Renderer>() ?? groundHit.collider.GetComponentInParent<Renderer>();
        if (renderer == null)
        {
            return false;
        }

        Material[] sharedMaterials = renderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            return false;
        }

        int materialIndex = 0;
        if (groundHit.collider is MeshCollider meshCollider && meshCollider.sharedMesh != null && groundHit.triangleIndex >= 0)
        {
            materialIndex = GetSubMeshIndex(meshCollider.sharedMesh, groundHit.triangleIndex);
        }

        materialIndex = Mathf.Clamp(materialIndex, 0, sharedMaterials.Length - 1);
        material = sharedMaterials[materialIndex];
        return material != null;
    }

    private bool TryGetMaterialFootstepSet(Material material, out MaterialFootstepSet footstepSet)
    {
        footstepSet = default;
        if (materialFootsteps == null || material == null)
        {
            return false;
        }

        for (int i = 0; i < materialFootsteps.Length; i++)
        {
            if (materialFootsteps[i].material != material)
            {
                continue;
            }

            footstepSet = materialFootsteps[i];
            return true;
        }

        return false;
    }

    private bool TryGetPhysicsMaterialFootstepSet(PhysicsMaterial physicsMaterial, out PhysicsMaterialFootstepSet footstepSet)
    {
        footstepSet = default;
        if (physicsMaterialFootsteps == null || physicsMaterial == null)
        {
            return false;
        }

        for (int i = 0; i < physicsMaterialFootsteps.Length; i++)
        {
            if (physicsMaterialFootsteps[i].physicsMaterial != physicsMaterial)
            {
                continue;
            }

            footstepSet = physicsMaterialFootsteps[i];
            return true;
        }

        return false;
    }

    private bool TryGetTerrainLayerFootstepSet(TerrainLayer terrainLayer, out TerrainLayerFootstepSet footstepSet)
    {
        footstepSet = default;
        if (terrainLayerFootsteps == null || terrainLayer == null)
        {
            return false;
        }

        for (int i = 0; i < terrainLayerFootsteps.Length; i++)
        {
            if (terrainLayerFootsteps[i].terrainLayer != terrainLayer)
            {
                continue;
            }

            footstepSet = terrainLayerFootsteps[i];
            return true;
        }

        return false;
    }

    private void EnsureFootstepAudioSource()
    {
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
        }

        if (footstepAudioSource == null)
        {
            footstepAudioSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureFootstepAudioSource(footstepAudioSource);
    }

    private void ConfigureFootstepAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = footstepSpatialBlend;
    }

    private static AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int validClipCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validClipCount++;
            }
        }

        if (validClipCount == 0)
        {
            return null;
        }

        int targetIndex = Random.Range(0, validClipCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (targetIndex == 0)
            {
                return clips[i];
            }

            targetIndex--;
        }

        return null;
    }

    private static int GetSubMeshIndex(Mesh mesh, int triangleIndex)
    {
        if (mesh == null || mesh.subMeshCount <= 1)
        {
            return 0;
        }

        int triangleOffset = 0;
        for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            var subMesh = mesh.GetSubMesh(subMeshIndex);
            int triangleCount = subMesh.indexCount / 3;
            if (triangleIndex < triangleOffset + triangleCount)
            {
                return subMeshIndex;
            }

            triangleOffset += triangleCount;
        }

        return 0;
    }

    private static float ResolveVolumeScale(float configuredVolume)
    {
        return configuredVolume <= 0.0001f ? 1f : configuredVolume;
    }

    private static float RandomRange(Vector2 range)
    {
        return Random.Range(range.x, range.y);
    }

    private static Vector2 SortVector(Vector2 range)
    {
        return range.x <= range.y ? range : new Vector2(range.y, range.x);
    }
}
