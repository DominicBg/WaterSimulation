using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

public class WaterMarchingCube
{
    static bool[,,] usedGrid;
    static int[,,] map;

   // static NativeArray<int> nativeMap;
   // static NativeArray<bool> nativeUsedGrid;
    static NativeList<int3> nativeUsedIndices;

    public static void Dispose()
    {
     //   nativeMap.Dispose();
    //    nativeUsedGrid.Dispose();
        nativeUsedIndices.Dispose();
    }

    /// <summary>
    /// Fills the Mesh based on the waterParticle[]
    /// </summary>
    public static void GenerateMesh(WaterParticle[] waterParticles, int count, float3 minPosition, float3 maxPosition, int resolution, ref Mesh mesh)
    {
        int resolutionCube = resolution * resolution * resolution;
        if (usedGrid == null || usedGrid.Length != resolutionCube)
        {
            usedGrid = new bool[resolution, resolution, resolution];
            map = new int[resolution, resolution, resolution];
        }
        else
        {
            Array.Clear(usedGrid, 0, resolutionCube);
            Array.Clear(map, 0, resolutionCube);
        }

        float invResolution = 1f / resolution;
        float step = math.distance(maxPosition.x, minPosition.x) * invResolution;
        float invStep = 1f / step;
        List<int3> usedIndices = new List<int3>(resolution * resolution);

        for (int i = 0; i < count; i++)
        {
            int3 index = GetPositionIndex(waterParticles[i].position, minPosition, maxPosition, resolution, invStep);

            //Only calculate marching cube for the 8 corners per cube
            for (int j = 0; j < MarchingCubeTables.CornerTable.Length; j++)
            {
                int3 conerIndex = index + MarchingCubeTables.CornerTable[j];
                if (!usedGrid[conerIndex.x, conerIndex.y, conerIndex.z])
                {
                    usedGrid[conerIndex.x, conerIndex.y, conerIndex.z] = true;
                    usedIndices.Add(conerIndex);
                }
                int3 conerIndex2 = index - MarchingCubeTables.CornerTable[j];
                if (!usedGrid[conerIndex2.x, conerIndex2.y, conerIndex2.z])
                {
                    usedGrid[conerIndex2.x, conerIndex2.y, conerIndex2.z] = true;
                    usedIndices.Add(conerIndex2);
                }
            }

            map[index.x, index.y, index.z] = 1;
        }

        //MarchingCube.CreateMeshData(map, usedIndices, invResolution, ref mesh);
    }


    public static void GenerateMesh(NativeArray<SPHSystem.WaterParticle> waterParticles, int count, float3 minPosition, float3 maxPosition, int resolution, ref Mesh mesh)
    {
        Profiler.BeginSample("WaterMarchingCube");
        int resolutionCube = resolution * resolution * resolution;
        if (usedGrid == null || usedGrid.Length != resolutionCube)
        {
            usedGrid = new bool[resolution, resolution, resolution];
            map = new int[resolution, resolution, resolution];
        }
        else
        {
            Array.Clear(usedGrid, 0, resolutionCube);
            Array.Clear(map, 0, resolutionCube);
        }

        float invResolution = 1f / resolution;
        float step = math.distance(maxPosition.x, minPosition.x) * invResolution;
        float invStep = 1f / step;
        NativeList<int3> usedIndices = new NativeList<int3>(resolution * resolution, Allocator.Temp);

        for (int i = 0; i < count; i++)
        {
            int3 index = GetPositionIndex(waterParticles[i].position, minPosition, maxPosition, resolution, invStep);

            //Only calculate marching cube for the 8 corners per cube
            for (int j = 0; j < MarchingCubeTables.CornerTable.Length; j++)
            {
                int3 conerIndex = index + MarchingCubeTables.CornerTable[j];
                if (!usedGrid[conerIndex.x, conerIndex.y, conerIndex.z])
                {
                    usedGrid[conerIndex.x, conerIndex.y, conerIndex.z] = true;
                    usedIndices.Add(conerIndex);
                }
                int3 conerIndex2 = index - MarchingCubeTables.CornerTable[j];
                if (!usedGrid[conerIndex2.x, conerIndex2.y, conerIndex2.z])
                {
                    usedGrid[conerIndex2.x, conerIndex2.y, conerIndex2.z] = true;
                    usedIndices.Add(conerIndex2);
                }
            }

            map[index.x, index.y, index.z] = 1;
        }

        Profiler.EndSample();
        MarchingCube.CreateMeshData(map, usedIndices, invResolution, ref mesh);
        usedIndices.Dispose();
    }

    /// <summary>
    /// Uncompleted Bursted version
    /// </summary>
    public static void GenerateMeshBurst(NativeArray<SPHSystem.WaterParticle> waterParticles, int count, float3 minPosition, float3 maxPosition, int resolution, ref Mesh mesh)
    {
        Profiler.BeginSample("WaterMarchingCube");

        int resolutionCube = resolution * resolution * resolution;
        //NativeArray<int> mapBurst = new NativeArray<int>(resolutionCube, Allocator.TempJob);
        //NativeList<int3> usedIndicesBurst = new NativeList<int3>(Allocator.TempJob);

        if (!nativeUsedIndices.IsCreated || nativeUsedIndices.Capacity != resolutionCube)
        {
            //nativeMap = new NativeArray<int>(resolutionCube, Allocator.Persistent);
            //nativeUsedGrid = new NativeArray<bool>(resolutionCube, Allocator.Persistent);
            nativeUsedIndices = new NativeList<int3>(resolutionCube, Allocator.Persistent);
        }
        else
        {
            //unsafe
            //{
            //    UnsafeUtility.MemClear(UnsafeUtility.AddressOf(ref nativeMap), (long)nativeMap.Length);
            //    UnsafeUtility.MemClear(UnsafeUtility.AddressOf(ref nativeUsedGrid), (long)nativeUsedGrid.Length * sizeof(bool));
            //}

            nativeUsedIndices.Clear();
        }
         var nativeMap = new NativeArray<int>(resolutionCube, Allocator.TempJob);
         var nativeUsedGrid = new NativeArray<bool>(resolutionCube, Allocator.TempJob);

       
        float invResolution = 1f / resolution;

        FillMapJob fillMapJob = new FillMapJob()
        {
            map = nativeMap,
            usedGrid = nativeUsedGrid,
            usedIndices = nativeUsedIndices,
            resolution = resolution,
            invResolution = invResolution,
            maxPosition = maxPosition,
            minPosition = minPosition,
            waterParticles = waterParticles,
            
        };
        fillMapJob.Run();

        Profiler.EndSample();
        MarchingCube.CreateMeshData(nativeMap, nativeUsedIndices, resolution, invResolution, ref mesh);
        nativeMap.Dispose();
        nativeUsedGrid.Dispose();
    }

    public struct FillMapJob : IJob
    {
        //input 
        public int resolution;
        public float invResolution;
        public float3 maxPosition;
        public float3 minPosition;       
        public NativeArray<SPHSystem.WaterParticle> waterParticles;
        public NativeArray<int> map;
        public NativeArray<bool> usedGrid;
        public NativeList<int3> usedIndices;
        
        public void Execute()
        {
            float step = math.distance(maxPosition.x, minPosition.x) * invResolution;
            float invStep = 1f / step;

            //NativeArray<bool> usedGrid = new NativeArray<bool>(resolution * resolution * resolution, Allocator.Temp);

            int count = waterParticles.Length;
            for (int i = 0; i < count; i++)
            {
                int3 index = GetPositionIndex(waterParticles[i].position, minPosition, maxPosition, resolution, invStep);
                int indexFlat = GetFlatIndex(index, resolution);

                //Multiple particle will be in the same grid, don't recompute 
                if (map[indexFlat] != 1)
                {
                    map[indexFlat] = 1;

                    //Only calculate marching cube for the 8 corners per cube
                    for (int j = 0; j < MarchingCubeTables.CornerTable.Length; j++)
                    {
                        int3 conerIndex = index + MarchingCubeTables.CornerTable[j];
                        int cornerIndexFlat = GetFlatIndex(conerIndex, resolution);
                        if (!usedGrid[cornerIndexFlat])
                        {
                            usedIndices.Add(conerIndex);
                            usedGrid[cornerIndexFlat] = true;
                        }
                        int3 conerIndex2 = index - MarchingCubeTables.CornerTable[j];
                        int cornerIndex2Flat = GetFlatIndex(conerIndex2, resolution);
                        if (!usedGrid[cornerIndex2Flat])
                        {
                            usedIndices.Add(conerIndex2);
                            usedGrid[cornerIndex2Flat] = true;
                        }
                    }
                }
            }
            //usedGrid.Dispose();
        }
    }

    public static int GetFlatIndex(int3 index, int resolution)
    {
        return index.x + resolution * (index.y + resolution * index.z);
    }

    public static int3 GetPositionIndex(float3 position, float3 minPosition, float3 maxPosition, int resolution, float inverseStep)
    {
        position = math.clamp(position, minPosition, maxPosition);

        //Offset to be at min = 0
        position = position - minPosition;

        int3 index = (int3)math.floor(position * inverseStep);
        return math.clamp(index, 2, resolution - 3);
    }
}
