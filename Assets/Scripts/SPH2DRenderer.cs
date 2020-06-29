using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static SPHSystem2DBurst;

public class SPH2DRenderer : MonoBehaviour
{
    public ParticleSystem particleSystem;
    ParticleSystem.Particle[] particles;
    public float particleRadius = 1;
    public Color color;

    public void ShowParticleEffect(NativeArray<WaterParticle2D> waterParticles)
    {
        if (waterParticles == null || particleSystem.particleCount != waterParticles.Length)
        {
            particles = new ParticleSystem.Particle[waterParticles.Length];
            for (int i = 0; i < waterParticles.Length; i++)
            {
                particles[i].startSize = particleRadius;
                particles[i].startColor = color;
            }
        }
        else
        {
            particleSystem.GetParticles(particles);
        }

        for (int i = 0; i < waterParticles.Length; i++)
        {
            float2 pos = waterParticles[i].position;
            particles[i].position = new float3(pos.x, pos.y, 0);
            particles[i].startSize = particleRadius;
            particles[i].startColor = color;
        }
        particleSystem.SetParticles(particles, waterParticles.Length);
    }
}
