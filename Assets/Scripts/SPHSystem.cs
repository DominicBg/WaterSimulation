using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class SPHSystem : MonoBehaviour
{
    NativeArray<WaterParticle> waterParticles;
    NativeArray<Box> boxes;

    public SPHSettings settings;
    public SPHMarchingCubeRenderer waterRenderer;

    [Header("spawn")]
    public int3 size;
    public float3 startPos;
    public float3 spacing;

    private void OnDestroy()
    {
        waterParticles.Dispose();
        boxes.Dispose();
    }

    protected void Start()
    {      
        InitParticles();

        BoxObject[] boxObjects = FindObjectsOfType<BoxObject>();
        boxes = new NativeArray<Box>(boxObjects.Length, Allocator.Persistent);
        for (int i = 0; i < boxObjects.Length; i++)
        {
            boxes[i] = boxObjects[i].box;
        }
    }

    private void InitParticles()
    {
        int length = size.x * size.y * size.z;
        waterParticles = new NativeArray<WaterParticle>(length, Allocator.Persistent);

        int i = 0;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    WaterParticle waterParticle = new WaterParticle();
                    waterParticle.position = startPos + spacing * new float3(x, y, z);
                    waterParticles[i] = waterParticle;
                    i++;
                }
            }
        }
    }

    protected void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            waterParticles.Dispose();
            InitParticles();
        }
        ComputeDensityPressureJob computeDensityPressure = new ComputeDensityPressureJob()
        {
            poly6 = settings.POLY6(),
            gazConst = settings.gasConstant,
            mass = settings.mass,
            radiusSq = settings.particleRadius * settings.particleRadius,
            restDensity = settings.restDensity,
            waterParticles = waterParticles
        };
        computeDensityPressure.Run(waterParticles.Length);

        ComputeForceJob computeForce = new ComputeForceJob()
        {
            spikyGrad = settings.CalculateSpikyGrad(),
            viscosityKernel = settings.CalculateViscosityKernel(),
            gravity = settings.gravity,
            mass = settings.mass,
            radius = settings.particleRadius,
            radiusSq = settings.particleRadius * settings.particleRadius,
            viscosity = settings.viscosity,
            waterParticles = waterParticles
        };
        computeForce.Run(waterParticles.Length);

        IntegrateJob integrate = new IntegrateJob()
        {
            deltaTime = Time.deltaTime,
            boxes = boxes,
            collisionElasticity = settings.collisionElasticity,
            waterParticles = waterParticles,
            particleSize = settings.particleRadius,
        };
        integrate.Run(waterParticles.Length);

        waterRenderer.RenderWater(waterParticles);
    }

 

    private void OnDrawGizmos()
    {
        for (int i = 0; i < waterParticles.Length; i++)
        {
            Gizmos.DrawSphere(waterParticles[i].position, 1);
        }
    }

    [BurstCompile]
    public struct ComputeDensityPressureJob : IJobParallelFor
    {
        public NativeArray<WaterParticle> waterParticles;
        public float poly6;
        public float radiusSq;
        public float mass;
        public float gazConst;
        public float restDensity;

        public void Execute(int index)
        {
            WaterParticle currentParticle = waterParticles[index];
            currentParticle.density = 0;
            for (int i = 0; i < waterParticles.Length; i++)
            {
                WaterParticle otherParticle = waterParticles[i];
                float3 diff = currentParticle.position - otherParticle.position;
                float sqrtDistance = math.lengthsq(diff);
                if (sqrtDistance < radiusSq)
                {
                    float x = radiusSq - sqrtDistance;
                    currentParticle.density += mass * poly6 * x * x * x;
                }
            }
            //The ideal gas law, relating pressure to the summed density
            currentParticle.pressure = gazConst * (currentParticle.density - restDensity);
            waterParticles[index] = currentParticle;
        }
    }

    [BurstCompile]
    public struct ComputeForceJob : IJobParallelFor
    {
        public NativeArray<WaterParticle> waterParticles;
        public float spikyGrad;
        public float viscosityKernel;
        public float viscosity;
        public float3 gravity;
        public float mass;
        public float radius;
        public float radiusSq;

        public void Execute(int index)
        {
            float3 forcePressure = 0;
            float3 forceViscosity = 0;
            WaterParticle currentParticle = waterParticles[index];

            for (int i = 0; i < waterParticles.Length; i++)
            {
                if (i == index)
                    continue;

                WaterParticle otherParticle = waterParticles[i];
                float3 diff = currentParticle.position - otherParticle.position;
                float sqrtDistance = math.lengthsq(diff);
                if (sqrtDistance < radiusSq)
                {
                    float distance = math.sqrt(sqrtDistance);

                    // compute pressure force contribution
                    float3 forcePressure1 = -math.normalize(diff) * mass;
                    float3 forcePressure2 = (currentParticle.pressure + otherParticle.pressure) / (2 * otherParticle.density) * spikyGrad;
                    float3 forcePressure3 = radius - distance;
                    forcePressure += forcePressure1 * forcePressure2 * forcePressure3 * forcePressure3;

                    // compute viscosity force contribution
                    float3 forceVisc1 = viscosity * mass;
                    float3 forceVisc2 = (otherParticle.velocity - currentParticle.velocity) / otherParticle.density;
                    float3 forceVisc3 = viscosityKernel * (radius - distance);
                    forceViscosity += forceVisc1 * forceVisc2 * forceVisc3;
                }
            }

            float3 forceGravity = gravity * currentParticle.density;
            currentParticle.force = forcePressure + forceViscosity + forceGravity;

            waterParticles[index] = currentParticle;
        }
    }

    [BurstCompile]
    public struct IntegrateJob : IJobParallelFor
    {
        public float deltaTime;
        public NativeArray<WaterParticle> waterParticles;
        public NativeArray<Box> boxes;
        public float collisionElasticity;
        public float particleSize;

        public void Execute(int index)
        {
            WaterParticle currentParticle = waterParticles[index];
            currentParticle.velocity += deltaTime * currentParticle.force / currentParticle.density;

            float3 previousPosition = currentParticle.position;
            float3 nextPosition = currentParticle.position + deltaTime * currentParticle.velocity;
            //currentParticle.position += deltaTime * currentParticle.velocity;

            //bound checking
            bool hasCollision = false;
            for (int i = 0; i < boxes.Length; i++)
            {
                if(boxes[i].LineBoxIntersection(previousPosition, nextPosition, out float ratio, out float3 normal))
                {
                    currentParticle.velocity = math.reflect(currentParticle.velocity, normal) * collisionElasticity;
                    currentParticle.position = previousPosition;//math.lerp(previousPosition, currentParticle.position, ratio) + (normal * particleSize * 1.1f);
                    hasCollision = true;
                    break;
                }
            }

            if (!hasCollision)
                currentParticle.position = nextPosition;

            waterParticles[index] = currentParticle;
        }
    }

    public struct WaterParticle
    {
        public float3 position;
        public float3 velocity;
        public float3 force;
        public float density;
        public float pressure;
    }
}
