﻿using System.Collections;
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
    public int iterationPerFrame = 5;
    public float rotationSpeed = 5;

    public SPH2DRenderer SPH2DRenderer;
    NativeArray<WaterParticle2D> particles;
    NativeArray<int> collisionArray;

    public float collisionScale = 10;
    private const int sizeArray = 10;

    public float rotation = 0;
    public Camera camera;

    int[,] colArray = new int[sizeArray, sizeArray]
    {
        {1,1,1,1,1,1,1,1,1,1},
        {1,0,1,0,0,0,0,0,0,1},
        {1,0,0,0,0,1,1,0,0,1},
        {1,0,1,1,0,1,1,1,0,1},
        {1,0,0,0,0,0,0,1,0,1},
        {1,0,0,0,0,0,0,0,0,1},
        {1,0,0,1,0,1,0,0,0,1},
        {1,0,1,0,0,1,1,0,0,1},
        {1,1,0,0,0,1,0,0,0,1},
        {1,1,1,1,1,1,1,1,1,1},
    };

    private void Start()
    {
        Restart();


        collisionArray = new NativeArray<int>(sizeArray * sizeArray, Allocator.Persistent);
        for (int y = 0; y < sizeArray; y++)
        {
            for (int x = 0; x < sizeArray; x++)
            {
                collisionArray[y * sizeArray + x] = colArray[x, y];
            }
        }
    }

    private void OnDestroy()
    {
        if (particles.IsCreated)
        {
            particles.Dispose();
            collisionArray.Dispose();
        }
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
    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.A))
        {
            rotation += Time.fixedDeltaTime * rotationSpeed;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            rotation -= Time.fixedDeltaTime * rotationSpeed;
        }

        math.sincos(math.radians(rotation), out float sin, out float cos);
        float2 gravity = new float2(Gravity * -sin, Gravity * cos);
        camera.transform.eulerAngles = new Vector3(0, 0, rotation);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Restart();
        }

        for (int i = 0; i < iterationPerFrame; i++)
        {
            CalculateFluidBehavior(gravity);
        }
        //CalculateStats();

        SPH2DRenderer.ShowParticleEffect(particles);
    }

    private void CalculateFluidBehavior(float2 gravity)
    {
        NativeMultiHashMap<int, int> hashMap = new NativeMultiHashMap<int, int>(particles.Length, Allocator.TempJob);
        NativeArray<int2> cellOffset = new NativeArray<int2>(GridHashUtilities.cell2DOffsets, Allocator.TempJob);
        NativeArray<WaterParticle2D> readOnlyParticles = new NativeArray<WaterParticle2D>(particles, Allocator.TempJob);

        CalculateConst();

        var handle = new HashPositionJob()
        {
            hashMap = hashMap.AsParallelWriter(),
            particles = particles,
            radius = particleRadius
        }.Schedule(particles.Length, 1);
        handle = new ComputeDensityPressureJob()
        {
            particles = particles,
            readOnlyParticles = readOnlyParticles,

            particleRadius = particleRadius,
            particleRadiusSquared = particleRadius * particleRadius,
            hashMap = hashMap,
            cellOffset = cellOffset,

            //Consts
            GAS_CONST = GAS_CONST,
            MASS = MASS,
            POLY6 = POLY6,
            REST_DENS = REST_DENS
        }.Schedule(particles.Length, 1, handle);

        handle = new CopyReadOnlyParticleJob()
        {
            particles = particles,
            readOnlyParticles = readOnlyParticles,
        }.Schedule(particles.Length, 1, handle);

        handle = new ComputeForceJob()
        {
            hashMap = hashMap,
            cellOffset = cellOffset,
            particles = particles,
            readOnlyParticles = readOnlyParticles,

            particleRadius = particleRadius,
            particleRadiusSquared = particleRadius * particleRadius,

            //consts
            GRAVITY = gravity,
            MASS = MASS,
            SPIKY_GRAD = SPIKY_GRAD,
            VISC = VISC,
            VISC_LAP = VISC_LAP
        }.Schedule(particles.Length, 1, handle);

        handle = new IntegrateJob()
        {
            particles = particles,
            particleRadius = particleRadius,
            deltaTime = Time.fixedDeltaTime / iterationPerFrame,

            collisionScale = collisionScale,
            colArray = collisionArray,

            //Consts
            BOUND_DAMPING = BOUND_DAMPING,
            VIEW_HEIGHT = VIEW_HEIGHT,
            VIEW_WIDTH = VIEW_WIDTH,
        }.Schedule(particles.Length, 1, handle);

        handle.Complete();

        hashMap.Dispose();
        cellOffset.Dispose();
        readOnlyParticles.Dispose();
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
    public struct HashPositionJob : IJobParallelFor
    {
        public NativeMultiHashMap<int, int>.ParallelWriter hashMap;
        public float radius;
        public NativeArray<WaterParticle2D> particles;

        public void Execute(int index)
        {
            float2 position = particles[index].position;
            int hash = GridHashUtilities.Hash(position, radius);
            hashMap.Add(hash, index);
        }
    }

    [BurstCompile]
    public struct CopyReadOnlyParticleJob : IJobParallelFor
    {
        public NativeArray<WaterParticle2D> particles;
        public NativeArray<WaterParticle2D> readOnlyParticles;

        public void Execute(int index)
        {
            readOnlyParticles[index] = particles[index];
        }
    }


    [BurstCompile]
    public struct ComputeDensityPressureJob : IJobParallelFor
    {
        public NativeArray<WaterParticle2D> particles;
        [ReadOnly] public NativeArray<WaterParticle2D> readOnlyParticles;
        [ReadOnly] public NativeMultiHashMap<int, int> hashMap;
        [ReadOnly] public NativeArray<int2> cellOffset;

        public float particleRadius;
        public float particleRadiusSquared;

        //Consts
        public float MASS;
        public float POLY6;
        public float GAS_CONST;
        public float REST_DENS;

        public void Execute(int index)
        {
            WaterParticle2D pi = particles[index];
            pi.density = 0;

            for (int i = 0; i < cellOffset.Length; i++)
            {
                float2 position = pi.position + (float2)cellOffset[i] * particleRadius;
                int bucketIndex = GridHashUtilities.Hash(position, particleRadius);

                NativeMultiHashMapIterator<int> iterator;
                bool hasValue = hashMap.TryGetFirstValue(bucketIndex, out int j, out iterator);

                while (hasValue)
                {
                    WaterParticle2D pj = readOnlyParticles[j]; // particles[j];

                    float2 diff = pj.position - pi.position;
                    float distanceSq = math.lengthsq(diff);
                    if (distanceSq < particleRadiusSquared)
                    {
                        float ratio = particleRadius - math.sqrt(distanceSq);
                        pi.density += MASS * POLY6 * ratio * ratio * ratio;

                        //float squaredRatio = particleRadiusSquared - distanceSq;
                        //pi.density += MASS * POLY6 * squaredRatio * squaredRatio * squaredRatio;
                    }

                    hasValue = hashMap.TryGetNextValue(out j, ref iterator);
                }
            }

            pi.pressure = GAS_CONST * (pi.density - REST_DENS);
            particles[index] = pi;
        }
    }


    [BurstCompile]
    public struct ComputeForceJob : IJobParallelFor
    {
        public NativeArray<WaterParticle2D> particles;
        [ReadOnly] public NativeArray<WaterParticle2D> readOnlyParticles;
        [ReadOnly] public NativeMultiHashMap<int, int> hashMap;
        [ReadOnly] public NativeArray<int2> cellOffset;

        public float particleRadius;
        public float particleRadiusSquared;
        public float MASS;
        public float SPIKY_GRAD;
        public float VISC;
        public float VISC_LAP;
        public float2 GRAVITY;

        public void Execute(int index)
        {
            float2 forcePressure = 0;
            float2 forceViscosity = 0;

            WaterParticle2D pi = particles[index];

            for (int i = 0; i < cellOffset.Length; i++)
            {
                float2 position = pi.position + (float2)cellOffset[i] * particleRadius;
                int bucketIndex = GridHashUtilities.Hash(position, particleRadius);

                NativeMultiHashMapIterator<int> iterator;
                bool hasValue = hashMap.TryGetFirstValue(bucketIndex, out int j, out iterator);

                while (hasValue)
                {
                    if (index == j)
                    {
                        hasValue = hashMap.TryGetNextValue(out j, ref iterator);
                        continue;
                    }

                    WaterParticle2D pj = readOnlyParticles[j]; // particles[j];
                    float2 diff = pj.position - pi.position;

                    float distanceSquared = math.lengthsq(diff);
                    if (distanceSquared < particleRadiusSquared)
                    {
                        float distance = math.sqrt(distanceSquared);
                        float radiusRatio = particleRadius - distance;
                        float2 normalizedDir = diff / distance;

                        forcePressure += -normalizedDir * MASS * (pi.pressure + pj.pressure) / (2f * pj.density) * SPIKY_GRAD * radiusRatio * radiusRatio;
                        forceViscosity += VISC * MASS * (pj.velocity - pi.velocity) / pj.density * VISC_LAP * (particleRadius - distance);
                    }
                    hasValue = hashMap.TryGetNextValue(out j, ref iterator);
                }
            }

            float2 forceFravity = GRAVITY * pi.density;
            pi.force = forcePressure + forceViscosity + forceFravity;
            particles[index] = pi;
        }
    }

    [BurstCompile]
    public struct IntegrateJob : IJobParallelFor
    {
        public NativeArray<WaterParticle2D> particles;
        [ReadOnly] public NativeArray<int> colArray;
        public float collisionScale;

        public float particleRadius;
        public float VIEW_WIDTH;
        public float VIEW_HEIGHT;
        public float BOUND_DAMPING;
        public float deltaTime;
        public void Execute(int i)
        {
            WaterParticle2D p = particles[i];
            p.velocity += deltaTime * p.force / p.density;

            float2 previousPosition = p.position;
            int2 prevPos = GridHashUtilities.Quantize(p.position, collisionScale);
            float2 step = deltaTime * p.velocity;
            p.position += step;

            float velocityMagnitude = math.length(p.velocity);
            if (velocityMagnitude < math.FLT_MIN_NORMAL)
            {
                particles[i] = p;

                return;
            }

            float2 radiusEdgePosition = p.position + (p.velocity / velocityMagnitude) * particleRadius;

            //find grid position
            int2 newPos = GridHashUtilities.Quantize(radiusEdgePosition, collisionScale);

            if(newPos.x < 0 || newPos.x >= sizeArray || newPos.y < 0 || newPos.y >= sizeArray)
            {
                //out of bound
                return;
            }


            bool movedInSameGrid = prevPos.x == newPos.x &&
                                   prevPos.y == newPos.y;
            bool hasCollision = colArray[newPos.y * sizeArray + newPos.x] == 1;
            if (!movedInSameGrid && hasCollision)
            {
                //left collision
                if (prevPos.x > newPos.x)
                {
                    p.velocity.x *= BOUND_DAMPING;
                    p.position.x = (prevPos.x * collisionScale) + particleRadius;
                }
                //right collision
                else if (prevPos.x < newPos.x)
                {
                    p.velocity.x *= BOUND_DAMPING;
                    p.position.x = (newPos.x * collisionScale) - particleRadius;
                }

                //bottom collision
                if (prevPos.y > newPos.y)
                {
                    p.velocity.y *= BOUND_DAMPING;
                    p.position.y = (prevPos.y * collisionScale) + particleRadius;
                }
                //up collision
                else if (prevPos.y < newPos.y)
                {
                    p.velocity.y *= BOUND_DAMPING;
                    p.position.y = (newPos.y * collisionScale) - particleRadius;
                }
            }

            //test if have collision

            //reflect velocity on normal?

            //replace position


            //if (p.position.x - particleRadius < -VIEW_WIDTH)
            //{
            //    p.velocity.x *= BOUND_DAMPING;
            //    p.position.x = -VIEW_WIDTH + particleRadius;
            //}
            //if (p.position.x + particleRadius > VIEW_WIDTH)
            //{
            //    p.velocity.x *= BOUND_DAMPING;
            //    p.position.x = VIEW_WIDTH - particleRadius;
            //}
            //if (p.position.y - particleRadius < -VIEW_HEIGHT)
            //{
            //    p.velocity.y *= BOUND_DAMPING;
            //    p.position.y = -VIEW_HEIGHT + particleRadius;
            //}
            //if (p.position.y + particleRadius > VIEW_HEIGHT)
            //{
            //    p.velocity.y *= BOUND_DAMPING;
            //    p.position.y = VIEW_HEIGHT - particleRadius;
            //}
            particles[i] = p;
        }
    }

    public float Gravity = -9.81f;
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

    private void OnDrawGizmos()
    {
        //for (int i = 0; i < particles.Length; i++)
        //{
        //    float2 pos = particles[i].position;
        //    Gizmos.DrawSphere(new float3(pos.x, pos.y, 0), particleRadius);
        //}

        for (int row = 0; row < sizeArray; row++)
        {
            for (int col = 0; col < sizeArray; col++)
            {
                if (colArray[row, col] == 1)
                {
                    Gizmos.DrawCube(
                        new float3(row * collisionScale, col * collisionScale, 0) + new float3(collisionScale / 2, collisionScale / 2, 0),
                        new float3(collisionScale, collisionScale, 0.01f));
                }
            }
        }

    }

    void CalculateConst()
    {
        POLY6 = 315f / (65f * math.PI * math.pow(particleRadius, 9f));
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
