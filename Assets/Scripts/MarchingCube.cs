using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

public static class MarchingCube
{
    const int MAXEDGE = 16;

    static List<int> triangles = new List<int>(100);
    static List<Vector3> vertices = new List<Vector3>(100);

    public static void CreateMeshData(int[,,] map, List<int3> usedIndices, float scaleFactor, ref Mesh mesh)
    {
        triangles.Clear();
        vertices.Clear();

        int[] corners = new int[8];
        
        for (int i = 0; i < usedIndices.Count; i++)
        {
            int3 index = usedIndices[i];
            for (int j = 0; j < corners.Length; j++)
            {
                int3 corner = index + MarchingCubeTables.CornerTable[j];
                corners[j] = map[corner.x, corner.y, corner.z];
            }
            MarchCube(index, corners, scaleFactor, triangles, vertices);
        } 
                
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        //might want to calculate the normal on the spot
        mesh.RecalculateNormals();
    }

    static void MarchCube(float3 position, int[] corners, float scaleFactor, List<int> triangles, List<Vector3> vertices)
    {
        int configIndex = GetConfigIndex(corners);

        //empty config
        if (configIndex == 0 || configIndex == 255)
            return;

        for (int edgeIndex = 0; edgeIndex < MAXEDGE; edgeIndex++)
        {
            int index = MarchingCubeTables.TriangleTable[configIndex, edgeIndex];

            //No more triangles
            if (index == -1)
                return;

            float3 vertex1 = position + MarchingCubeTables.EdgeTable[index, 0];
            float3 vertex2 = position + MarchingCubeTables.EdgeTable[index, 1];

            //TODO Include smooth lerp
            float3 vertexPosition = math.lerp(vertex1, vertex2, 0.5f) * scaleFactor;

            vertices.Add(vertexPosition);
            triangles.Add(vertices.Count - 1);      
        }
    }

    static int GetConfigIndex(int[] corners)
    {
        int configIndex = 0;
        configIndex |= (int)math.sign(corners[0]) << 0;
        configIndex |= (int)math.sign(corners[1]) << 1;
        configIndex |= (int)math.sign(corners[2]) << 2;
        configIndex |= (int)math.sign(corners[3]) << 3;
        configIndex |= (int)math.sign(corners[4]) << 4;
        configIndex |= (int)math.sign(corners[5]) << 5;
        configIndex |= (int)math.sign(corners[6]) << 6;
        configIndex |= (int)math.sign(corners[7]) << 7;
        return configIndex;
    }

}
