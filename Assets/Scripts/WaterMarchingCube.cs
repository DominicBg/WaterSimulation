using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class WaterMarchingCube
{
    static bool[,,] usedGrid;
    static int[,,] map;

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

        MarchingCube.CreateMeshData(map, usedIndices, invResolution, ref mesh);
    }

    public static void GenerateMesh(NativeArray<SPHSystem.WaterParticle> waterParticles, int count, float3 minPosition, float3 maxPosition, int resolution, ref Mesh mesh)
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

        MarchingCube.CreateMeshData(map, usedIndices, invResolution, ref mesh);
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
