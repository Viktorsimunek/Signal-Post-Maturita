using UnityEngine;

public class InteriorDustFollow : MonoBehaviour
{
    ParticleSystem[] systems;
    ParticleSystem.Particle[] particles;

    void OnEnable()
    {
        systems = GetComponentsInChildren<ParticleSystem>();
        int maxParticles = 0;
        foreach (var system in systems)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            maxParticles = Mathf.Max(maxParticles, main.maxParticles);
            system.Play(false);
        }
        particles = new ParticleSystem.Particle[maxParticles];
    }

    void LateUpdate()
    {
        if (Time.deltaTime <= 0) return;
        foreach (var system in systems)
        {
            var area = new Bounds(Vector3.zero, system.shape.scale);
            int count = system.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = system.transform.InverseTransformPoint(particles[i].position);
                if (area.Contains(position)) continue;

                position = new Vector3(
                    Random.Range(area.min.x, area.max.x),
                    Random.Range(area.min.y, area.max.y),
                    Random.Range(area.min.z, area.max.z));
                particles[i].position = system.transform.TransformPoint(position);
            }
            system.SetParticles(particles, count);
        }
    }
}
