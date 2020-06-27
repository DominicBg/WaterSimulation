using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class SPHSystem2DBurst : MonoBehaviour
{
    public int2 count2 = 10;
    public float2 scale = 1;
    public float2 startPos = new float2(0, 5);
    int count;

    NativeArray<WaterParticle2D> particles;
    private void Start()
    {
        Restart();
    }

    private void OnDestroy()
    {
        if (particles.IsCreated)
            particles.Dispose();
    }

    void Restart()
    {
        count = count2.x * count2.y;

        if (particles.IsCreated)
            particles.Dispose();

        particles = new NativeArray<WaterParticle2D>(count, Allocator.Persistent);
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

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            Restart();
        }

        CalculateConst();
        new ComputeDensityPressureJob()
        {
            GAS_CONST = GAS_CONST,
            particleRadiusSquared = particleRadiusSquared,
            MASS = MASS,
            particles = particles,
            POLY6 = POLY6,
            REST_DENS = REST_DENS
        }.Run(particles.Length);

        ComputeForceJob computeForceJob = new ComputeForceJob()
        {
            G = G,
            particleRadius = particleRadius,
            particleRadiusSquared = particleRadiusSquared,
            MASS = MASS,
            particles = particles,
            SPIKY_GRAD = SPIKY_GRAD,
            VISC = VISC,
            VISC_LAP = VISC_LAP
        };
        computeForceJob.Run(particles.Length);
        
        new IntegrateJob()
        {
            particles = particles,
            BOUND_DAMPING = BOUND_DAMPING,
            EPS = EPS,
            particleRadius = particleRadius,
            VIEW_HEIGHT = VIEW_HEIGHT,
            VIEW_WIDTH = VIEW_WIDTH,
            deltaTime = Time.deltaTime
        }.Run(particles.Length);

        //CalculateStats();
    }

    void CalculateStats()
    {
        float density = 0;
        float pressure = 0;

        float invLength = 1f / particles.Length;

        for (int i = 0; i < particles.Length; i++)
        {
            density += particles[i].density * invLength;
            pressure += particles[i].pressure * invLength;
        }

        Debug.Log($"Mean Density {density}, mean pressure {pressure}");
    }

    [BurstCompile]
    public struct ComputeDensityPressureJob : IJobParallelFor
    {
        public NativeArray<WaterParticle2D> particles;
        public float particleRadiusSquared;
        public float MASS;
        public float POLY6;
        public float GAS_CONST;
        public float REST_DENS;

        public void Execute(int i)
        {
            WaterParticle2D pi = particles[i];
            pi.density = 0;
            for (int j = 0; j < particles.Length; j++)
            {
                WaterParticle2D pj = particles[j];
                float2 rij = pj.position - pi.position;
                float distanceSq = math.lengthsq(rij);
                if (distanceSq < particleRadiusSquared)
                {
                    float squaredRatio = particleRadiusSquared - distanceSq;
                    pi.density += MASS * POLY6 * squaredRatio * squaredRatio * squaredRatio;
                }

            }
            //pi.p = GAS_CONST2 * (math.pow(pi.rho / REST_DENS, 7) - 1);
            pi.pressure = GAS_CONST * (pi.density - REST_DENS);
            particles[i] = pi;        
        }
    }


    [BurstCompile]
    public struct ComputeForceJob : IJobParallelFor
    {
        public NativeArray<WaterParticle2D> particles;
        public float particleRadius;
        public float particleRadiusSquared;
        public float MASS;
        public float SPIKY_GRAD;
        public float VISC;
        public float VISC_LAP;
        public float2 G;

        public void Execute(int i)
        {
            float2 fpress = 0;
            float2 fvisc = 0;

            WaterParticle2D pi = particles[i];
            for (int j = 0; j < particles.Length; j++)
            {
                WaterParticle2D pj = particles[j];
                if (i == j)
                    continue;

                float2 rij = pj.position - pi.position;
                float rSq = math.lengthsq(rij);
                if(rSq < particleRadiusSquared)
                {
                    float r = math.sqrt(rSq);
                    float radiusRatio = particleRadius - r;
                    fpress += -math.normalize(rij) * MASS * (pi.pressure + pj.pressure) / (2f * pj.density) * SPIKY_GRAD * radiusRatio * radiusRatio;
                    fvisc += VISC * MASS * (pj.velocity - pi.velocity) / pj.density * VISC_LAP * (particleRadius - r);
                }
            }

            float2 fgrav = G * pi.density;
            pi.force = fpress + fvisc + fgrav;
            particles[i] = pi;
        }
    }

    [BurstCompile]
    public struct IntegrateJob : IJobParallelFor
    {
        public NativeArray<WaterParticle2D> particles;
        public float particleRadius;
        public float VIEW_WIDTH;
        public float VIEW_HEIGHT;
        public float EPS;
        public float BOUND_DAMPING;
        public float deltaTime;
        public void Execute(int i)
        {
            WaterParticle2D p = particles[i];
            p.velocity += deltaTime * p.force / p.density;
            p.position += deltaTime * p.velocity;

            if (p.position.x - EPS < 0.0f)
            {
                p.velocity.x *= BOUND_DAMPING;
                p.position.x = EPS;
            }
            if (p.position.x + EPS > VIEW_WIDTH)
            {
                p.velocity.x *= BOUND_DAMPING;
                p.position.x = VIEW_WIDTH - EPS;
            }
            if (p.position.y - EPS < 0.0f)
            {
                p.velocity.y *= BOUND_DAMPING;
                p.position.y = EPS;
            }
            if (p.position.y + EPS > VIEW_HEIGHT)
            {
                p.velocity.y *= BOUND_DAMPING;
                p.position.y = VIEW_HEIGHT - EPS;
            }
            particles[i] = p;
        }
    }

    public float2 G = new float2(0, -9.81f);
    public float particleRadius = 1.6f;
    public float MASS = 6.5f;
    public float BOUND_DAMPING = -0.5f;
    public float REST_DENS = 75;
    public float GAS_CONST = 75;
    public float VISC = 25;
    public float VIEW_HEIGHT = 255;
    public float VIEW_WIDTH = 255;
    public float DT = 0.0008f;
    public float GAS_CONST2 = 25f;

    float VISC_LAP;
    float POLY6;
    float SPIKY_GRAD;
    float particleRadiusSquared;
    float EPS;

    private void OnDrawGizmos()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            float2 pos = particles[i].position;
            Gizmos.DrawSphere(new float3(pos.x, pos.y, 0), particleRadius);
        }
    }

    void CalculateConst()
    {
        POLY6 = 315f / (65f * math.PI* math.pow(particleRadius, 9f));
        particleRadiusSquared = particleRadius * particleRadius;
        EPS = particleRadius;
        SPIKY_GRAD = -45f / (math.PI * math.pow(particleRadius, 6f));
        VISC_LAP = 45f / (math.PI * math.pow(particleRadius, 6f));
    }


    public struct WaterParticle2D
    {
        public float2 position;
        public float2 velocity;
        public float2 force;
        public float density;
        public float pressure;
    }
}
