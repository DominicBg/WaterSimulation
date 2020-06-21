using System;
using Unity.Mathematics;
using UnityEngine;

public class WaterSystem : MonoBehaviour
{
    public int particleCount = 100;
    public float particleRadius = 1;
    public float particleMass = 1;
    [Range(0,1)] public float elasticity = 0.9f;
    public float3 gravity = new float3(0, 9.81f, 0);
    public float intervalDroplet = .2f;

    public int marchingCubeResolution;
    public int maxWaterParticlePerZone = 10;

    bool isActive = false;
    public Transform emitterPosition;
    public ParticleSystem particleSystem;
    public MeshFilter waterMeshFilter;
    public BoxCollider cubeMarchineZone;
    ParticleSystem.Particle[] particles;

    Plane[] planes;
    Box[] boxes;
    WaterParticle[] waterParticles;
    int currentParticleCount = 0;
    float currentInterval = 0;
    Mesh mesh;



    private void Start()
    {
        StartSimulation();

    }

    private void Update()
    {
        if (!isActive)
            return;

        CalculateMinMaxPositions(out float3 minPosition, out float3 maxPosition);

        CheckSpawnWaterParticle();
        UpdateAcceleration();
        UpdateVelocity();
        UpdateInterWaterParticleCollision(minPosition, maxPosition);
        UpdateStaticCollisions();
        UpdatePositions();
        ShowParticleEffect();
        RenderWater(minPosition, maxPosition);
    }

    private void CheckSpawnWaterParticle()
    {
        currentInterval += Time.deltaTime;
        if (currentParticleCount < particleCount && currentInterval > intervalDroplet)
        {
            waterParticles[currentParticleCount].position = emitterPosition.position;
            waterParticles[currentParticleCount].velocity = gravity;
            currentParticleCount++;
            currentInterval -= intervalDroplet;
        }
    }

    public void StartSimulation()
    {
        waterParticles = new WaterParticle[particleCount];
        isActive = true;

        PlaneObject[] planeObjects = FindObjectsOfType<PlaneObject>();
        planes = new Plane[planeObjects.Length];
        for (int i = 0; i < planes.Length; i++)
        {
            planes[i] = planeObjects[i].plane;
        }

        BoxObject[] boxObjects = FindObjectsOfType<BoxObject>();
        boxes = new Box[boxObjects.Length];
        for (int i = 0; i < boxObjects.Length; i++)
        {
            //boxes[i] = boxObjects[i].box;
        }

        mesh = new Mesh();
    }

    void UpdateAcceleration()
    {
        for (int i = 0; i < currentParticleCount; i++)
        {
            WaterParticle particle = waterParticles[i];
            particle.acceleration += gravity * Time.deltaTime;
            waterParticles[i] = particle;
        }
    }

    void UpdateVelocity()
    {
        for (int i = 0; i < currentParticleCount; i++)
        {
            WaterParticle particle = waterParticles[i];
            particle.velocity += particle.acceleration * Time.deltaTime;
            waterParticles[i] = particle;
        }
    }

    void UpdatePositions()
    {
        for (int i = 0; i < currentParticleCount; i++)
        {
            WaterParticle particle = waterParticles[i];
            particle.position += particle.velocity * Time.deltaTime;
            waterParticles[i] = particle;
        }
    }

    void UpdateStaticCollisions()
    {
        for (int i = 0; i < currentParticleCount; i++)
        {
            WaterParticle particle = waterParticles[i];

            if (math.lengthsq(particle.velocity) < 0.01)
                continue;

            float3 startPosition = particle.position;
            float3 nextPosition = particle.position + particle.velocity * Time.deltaTime;

            for (int j = 0; j < boxes.Length; j++)
            {
                if (boxes[j].TestCollision(startPosition, nextPosition, out float ratio, out float3 normal))
                {
                    particle.velocity = math.reflect(particle.velocity, normal) * elasticity;
                    float3 dir = math.normalize(startPosition - nextPosition);
                    particle.position = math.lerp(startPosition, nextPosition, ratio) + (dir * particleRadius);
                    break;
                }
            }
            waterParticles[i] = particle;
        }
    }

    int3[] particleIndicesInCollisionTensor;
    short[,,,] waterParticleCollisionTensor;
    byte[,,] waterParticleCountPerVoxel;
    void UpdateInterWaterParticleCollision(float3 minPosition, float3 maxPosition)
    {
        //cache it
        int resolutionCube = marchingCubeResolution * marchingCubeResolution * marchingCubeResolution;
        if(waterParticleCountPerVoxel == null || waterParticleCountPerVoxel.Length != resolutionCube)
        {
            waterParticleCollisionTensor = new short[marchingCubeResolution, marchingCubeResolution, marchingCubeResolution, maxWaterParticlePerZone];
            waterParticleCountPerVoxel = new byte[marchingCubeResolution, marchingCubeResolution, marchingCubeResolution];
            particleIndicesInCollisionTensor = new int3[particleCount];
        }
        else
        {
            Array.Clear(waterParticleCollisionTensor, 0, resolutionCube * maxWaterParticlePerZone);
            Array.Clear(waterParticleCountPerVoxel, 0, resolutionCube );
            Array.Clear(particleIndicesInCollisionTensor, 0, particleCount);
        }

        //float3 minPosition = cubeMarchineZone.bounds.min;
        //float3 maxPosition = cubeMarchineZone.bounds.max;
        float invResolution = 1f / marchingCubeResolution;
        float step = math.distance(minPosition, maxPosition) * invResolution;
        float invStep = 1f / step;

        //Broad phase
        for (int i = 0; i < particleCount; i++)
        {
            WaterParticle particle = waterParticles[i];
            int3 index = WaterMarchingCube.GetPositionIndex(particle.position, minPosition, maxPosition, marchingCubeResolution, invStep);
            int numberOfParticleInVoxel = waterParticleCountPerVoxel[index.x, index.y, index.z];
            if (numberOfParticleInVoxel < maxWaterParticlePerZone)
            {
                //Store the index of the particle in the grid
                particleIndicesInCollisionTensor[i] = index;
                waterParticleCollisionTensor[index.x, index.y, index.z, numberOfParticleInVoxel] = (short)i;
                waterParticleCountPerVoxel[index.x, index.y, index.z]++;
            }
        }

        //Narrow phase
        for (int i = 0; i < particleCount; i++)
        {
            //TODO check all 26 adjacent cells

            WaterParticle currentParticle = waterParticles[i];
            int3 index = particleIndicesInCollisionTensor[i];
            int numberOfParticleInVoxel = waterParticleCountPerVoxel[index.x, index.y, index.z];
            for (int j = 0; j < numberOfParticleInVoxel; j++)
            {
                int otherParticleIndex = waterParticleCollisionTensor[index.x, index.y, index.z, j];

                if (i == otherParticleIndex)
                    break;

                WaterParticle otherParticle = waterParticles[otherParticleIndex];
                float3 diff = otherParticle.position - currentParticle.position;

                //No collision
                if (math.lengthsq(diff) > particleRadius * particleRadius)
                    continue;

                //TODO custom burstable functions
                //On collision vector keep they rejection, but swap projections
                float3 currentProjection = Vector3.Project(currentParticle.velocity, diff);
                float3 currentRejection = currentParticle.velocity - currentProjection;

                float3 otherProjection = Vector3.Project(otherParticle.velocity, diff);
                float3 otherRejection = currentParticle.velocity - otherProjection;

                //Swap
                currentParticle.velocity = (currentRejection + otherProjection) * elasticity;
                otherParticle.velocity = (otherRejection + currentProjection) * elasticity;

                //currentParticle.position -= diff * particleRadius;
                //otherParticle.position += diff * particleRadius;

                waterParticles[i] = currentParticle;
                waterParticles[otherParticleIndex] = otherParticle;
            }
        }
    }

    void ShowParticleEffect()
    {
        if (particles == null || particleSystem.particleCount != particleCount)
        {
            particles = new ParticleSystem.Particle[particleCount];
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].startSize = particleRadius;
                particles[i].startColor = Color.blue;
            }
        }
        else
        {
            particleSystem.GetParticles(particles);
        }

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].position = waterParticles[i].position;
        }
        particleSystem.SetParticles(particles, particleCount);
    }

    void RenderWater(float3 minPosition, float3 maxPosition)
    {
     
        //WaterMarchingCube.GenerateMesh(waterParticles, currentParticleCount, minPosition, maxPosition, marchingCubeResolution, ref mesh);
        //waterMeshFilter.mesh = mesh;
        // waterMeshFilter.transform.localScale = Vector3.one * cubeMarchineZone.size.x;
        //waterMeshFilter.transform.localPosition = -Vector3.one * cubeMarchineZone.size.x * 0.5f;
        //float3 diff = maxPosition - minPosition;
        //float scale = math.max(diff.x, math.max(diff.y, diff.z));
        //waterMeshFilter.transform.localScale = Vector3.one * scale;
        //waterMeshFilter.transform.localPosition = -diff * 0.5f;
    }

    void CalculateMinMaxPositions(out float3 minPosition, out float3 maxPosition)
    {
        //the dynamic is not accurate
        minPosition = cubeMarchineZone.bounds.min;
        maxPosition = cubeMarchineZone.bounds.max;
        return;


        minPosition = waterParticles[0].position;
        maxPosition = waterParticles[0].position;
        for (int i = 1; i < currentParticleCount; i++)
        {
            minPosition = math.min(minPosition, waterParticles[i].position);
            maxPosition = math.max(maxPosition, waterParticles[i].position);
        }
    }

}
