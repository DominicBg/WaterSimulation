using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class PBHSystem : MonoBehaviour
{
    public int2 count2 = 10;
    public float2 scale = 1;
    public float2 startPos = new float2(0, 5);
    int count;
    NativeArray<WaterParticle> particles;
    public int iteration = 1;

    private void Start()
    {
        Restart();
    }
    void Restart()
    {
        count = count2.x * count2.y;

        if (particles.IsCreated)
            particles.Dispose();

        particles = new NativeArray<WaterParticle>(count, Allocator.Persistent);
        int i = 0;
        for (int x = 0; x < count2.x; x++)
        {
            for (int y = 0; y < count2.x; y++)
            {
                var particle = particles[i];
                particle.position = new float2(startPos.x + x * scale.x, startPos.y + y * scale.y);
                particles[i] = particle;
                i++;
            }
        }
    }

    void Update()
    {
        ApplyForcePredictPosition();
        //Find Neightbour / hash pos

        for (int i = 0; i < iteration; i++)
        {
            CalculateLambda();
            CalculateDeltaPressure();
            UpdatePredictedPosition();
        }

        UpdateVelocityVerocityViscosityUpdatePosition();
    }


    void ApplyForcePredictPosition()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            WaterParticle particle = particles[i];
            particle.velocity += new float2(0, -9.81f) * Time.deltaTime;
            particle.predictedPosition += Time.deltaTime * particle.velocity;
            particles[i] = particle;
        }
    }

    void CalculateLambda()
    {
        float POLY6 = 315f / (65f * math.PI * math.pow(particleRadius, 9f));


        for (int i = 0; i < particles.Length; i++)
        {
            WaterParticle pi = particles[i];

            for (int j = 0; j < particles.Length; j++)
            {
                WaterParticle pj = particles[i];
                float2 diff = pj.position - pi.position;
                float distanceSq = math.lengthsq(diff);
                if (distanceSq < particleRadius * particleRadius)
                {
                    float squaredRatio = particleRadius - distanceSq;
                    pi.density += mass * POLY6 * squaredRatio * squaredRatio * squaredRatio;
                }
            }
            //particle.density = Sum_j -> mass_jj * Kernel(pi-pj, h); 
            float constraint = (pi.density / restDensity) - 1;

        }
    }

    void CalculateDeltaPressure()
    {
        for (int i = 0; i < particles.Length; i++)
        {

            //and collision?
        }
    }

    void UpdatePredictedPosition()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            WaterParticle particle = particles[i];
            particle.predictedPosition += particle.deltaPressure;
        }
    }

    void UpdateVelocityVerocityViscosityUpdatePosition()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            WaterParticle particle = particles[i];
            particle.velocity = (1f / Time.deltaTime) * (particle.predictedPosition - particle.position);

            //apply vorcity confinement and XSPH viscosity

            particle.position = particle.predictedPosition;
        }
    }

    public float mass = 6.5f;
    public float restDensity = 1000;
    public float particleRadius = 1.6f;

    public struct WaterParticle
    {
        public float2 position;
        public float2 predictedPosition;
        public float2 velocity;
        public float density;
        public float lambda;
        public float deltaPressure;
    }
}
