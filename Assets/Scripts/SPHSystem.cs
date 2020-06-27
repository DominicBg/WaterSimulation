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
    NativeArray<Sphere> spheres;
    NativeArray<InnerBox> innerBoxes;


    public SPHSettings settings;
    public SPHMarchingCubeRenderer waterRenderer;

    [Header("spawn")]
    public int3 size;
    public float3 startPos;
    public float3 spacing;
    public float3 boxSize = 25;

    private void OnDestroy()
    {
        Dispose();
    }

    void Dispose()
    {
        waterParticles.Dispose();
        boxes.Dispose();
        spheres.Dispose();
        innerBoxes.Dispose();
    }

    protected void Start()
    {      
        InitParticles();
        SetColliders();
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

    private void SetColliders()
    {
        BoxObject[] boxObjects = FindObjectsOfType<BoxObject>();
        boxes = new NativeArray<Box>(boxObjects.Length, Allocator.Persistent);
        for (int i = 0; i < boxObjects.Length; i++)
        {
            boxes[i] = (Box)boxObjects[i].GetCollision();
        }

        SphereObject[] sphereObjects = FindObjectsOfType<SphereObject>();
        spheres = new NativeArray<Sphere>(sphereObjects.Length, Allocator.Persistent);
        for (int i = 0; i < sphereObjects.Length; i++)
        {
            spheres[i] = (Sphere)sphereObjects[i].GetCollision();
        }

        InnerBoxObject[] innerBoxObjects = FindObjectsOfType<InnerBoxObject>();
        innerBoxes = new NativeArray<InnerBox>(innerBoxObjects.Length, Allocator.Persistent);
        for (int i = 0; i < innerBoxObjects.Length; i++)
        {
            innerBoxes[i] = (InnerBox)innerBoxObjects[i].GetCollision();
        }
    }


    void CalculateStats()
    {
        float density = 0;
        float pressure = 0;

        float invLength = 1f / waterParticles.Length;

        for (int i = 0; i < waterParticles.Length; i++)
        {
            density += waterParticles[i].density * invLength;
            pressure += waterParticles[i].pressure * invLength;
        }

        Debug.Log($"Mean Density {density}, mean pressure {pressure}");
    }
    protected void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Dispose();
            InitParticles();
            SetColliders();
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
            spheres = spheres,
            innerBoxes = innerBoxes,
            collisionElasticity = settings.collisionElasticity,
            waterParticles = waterParticles,
            particleSize = settings.particleRadius,
            boxSize = boxSize
        };
        integrate.Run(waterParticles.Length);

        waterRenderer.RenderWater(waterParticles);
        //CalculateStats();
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
                float3 diff = otherParticle.position - currentParticle.position;
                float sqrtDistance = math.lengthsq(diff);
                if (sqrtDistance < radiusSq)
                {
                    //float x = radiusSq - sqrtDistance;
                    currentParticle.density += mass * poly6 * math.pow(radiusSq - sqrtDistance, 3);//;x * x * x;
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
                float3 diff = otherParticle.position - currentParticle.position;
                float sqrtDistance = math.lengthsq(diff);
                if (sqrtDistance < radiusSq)
                {
                    float distance = math.sqrt(sqrtDistance);
                    float3 ratioDistance = radius - distance;

                    // compute pressure force contribution
                    float3 forcePressure1 = -math.normalize(diff) * mass;
                    float3 forcePressure2 = (currentParticle.pressure + otherParticle.pressure) / (2 * otherParticle.density);
                    forcePressure += forcePressure1 * forcePressure2 * spikyGrad * (ratioDistance * ratioDistance);

                    // compute viscosity force contribution
                    //float3 forceVisc1 = viscosity * mass;
                    float3 forceVisc1 = viscosity * mass;
                    float3 forceVisc2 = (otherParticle.velocity - currentParticle.velocity) / otherParticle.density;
                    float3 forceVisc3 = viscosityKernel * ratioDistance;
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
        public NativeArray<Sphere> spheres;
        public NativeArray<InnerBox> innerBoxes;
        public float collisionElasticity;
        public float particleSize;
        public float3 boxSize;

        public void Execute(int index)
        {
            WaterParticle currentParticle = waterParticles[index];
            currentParticle.velocity += deltaTime * currentParticle.force / currentParticle.density;

            float3 previousPosition = currentParticle.position;
            float3 nextPosition = currentParticle.position + deltaTime * currentParticle.velocity;
            
            //Might get corrected by collision
            currentParticle.position += deltaTime * currentParticle.velocity;


            GetAxisBound(ref currentParticle.position.x, ref currentParticle.velocity.x, boxSize.x);
            GetAxisBound(ref currentParticle.position.y, ref currentParticle.velocity.y, boxSize.y);
            GetAxisBound(ref currentParticle.position.z, ref currentParticle.velocity.z, boxSize.z);

            //const float BOUND_DAMPING = -.5f;
            //if (currentParticle.position.x - particleSize < 0.0f)
            //{
            //    currentParticle.velocity.x *= BOUND_DAMPING;
            //    currentParticle.position.x = particleSize;
            //}
            //if (currentParticle.position.x + particleSize > boxSize.x)
            //{
            //    currentParticle.velocity.x *= BOUND_DAMPING;
            //    currentParticle.position.x = boxSize.x - particleSize;
            //}
            //if (currentParticle.position.y - particleSize < 0.0f)
            //{
            //    currentParticle.velocity.y *= BOUND_DAMPING;
            //    currentParticle.position.y = particleSize;
            //}
            //if (currentParticle.position.y + particleSize > boxSize.y)
            //{
            //    currentParticle.velocity.y *= BOUND_DAMPING;
            //    currentParticle.position.y = boxSize.y - particleSize;
            //}

            ////bound checking
            //for (int i = 0; i < boxes.Length; i++)
            //    if(boxes[i].TestCollision(previousPosition, nextPosition, out float ratio, out float3 normal))
            //        HandleCollision(ref currentParticle, previousPosition, nextPosition, ratio, normal);


            //for (int i = 0; i < spheres.Length; i++)
            //    if (spheres[i].TestCollision(previousPosition, nextPosition, out float ratio, out float3 normal))
            //        HandleCollision(ref currentParticle, previousPosition, nextPosition, ratio, normal);

            //for (int i = 0; i < innerBoxes.Length; i++)
            //    if (innerBoxes[i].TestCollision(previousPosition, nextPosition, out float ratio, out float3 normal))
            //        HandleCollision(ref currentParticle, previousPosition, nextPosition, ratio, normal);

            waterParticles[index] = currentParticle;
        }

        public void GetAxisBound(ref float position, ref float velocity, float limit)
        {
            const float BOUND_DAMPING = -.5f;
            if (position - particleSize < -limit)
            {
                velocity *= BOUND_DAMPING;
                position = -limit + particleSize;
            }
            if (position + particleSize > limit)
            {
                velocity *= BOUND_DAMPING;
                position = limit - particleSize;
            }
        }

        void HandleCollision(ref WaterParticle currentParticle, float3 previousPosition, float3 nextPosition, float ratio, float3 normal)
        {
            currentParticle.velocity = math.reflect(currentParticle.velocity, normal) * collisionElasticity;
            currentParticle.position = previousPosition;//math.lerp(previousPosition, currentParticle.position, ratio) + (normal * particleSize * 1.1f);
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
