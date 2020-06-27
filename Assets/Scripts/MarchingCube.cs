using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

public static class MarchingCube
{
    const int MAXTRI = 3;
    const int MAXEDGE = 5;

    static List<int> triangles = new List<int>(100);
    static List<Vector3> vertices = new List<Vector3>(100);


    public static void CreateMeshData(float[,,] map, NativeList<int3> usedIndices, float scaleFactor, ref Mesh mesh)
    {
        if (!MarchingCubeTables.IsInitialized)
            MarchingCubeTables.Initialize();


        Profiler.BeginSample("MarchingCube");

        triangles.Clear();
        vertices.Clear();
        
        for (int i = 0; i < usedIndices.Length; i++)
        {
            MarchCube(usedIndices[i], map, scaleFactor, triangles, vertices);
        } 
                
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);

        //might want to calculate the normal on the spot
        mesh.RecalculateNormals();
        Profiler.EndSample();
    }

    static void MarchCube(int3 index, float[,,] map, float scaleFactor, List<int> triangles, List<Vector3> vertices)
    {
        NativeArray<float> corners = new NativeArray<float>(8, Allocator.Temp);
        for (int j = 0; j < corners.Length; j++)
        {
            //int3 corner = index + MarchingCubeTables.CornerTable[j];
            int3 corner = index + MarchingCubeTables.NativeCornerTable[j];
            corners[j] = map[corner.x, corner.y, corner.z];
        }

        int configIndex = GetConfigIndex(corners);

        //empty config
        if (configIndex == 0 || configIndex == 255)
        {
            corners.Dispose();
            return;
        }

        const int maxEdges = 15;
        for (int edgeIndex = 0; edgeIndex < maxEdges; edgeIndex++)
        {
            //int triIndex = MarchingCubeTables.TriangleTable[configIndex, edgeIndex];
            int configIndexFlat = configIndex * MarchingCubeTables.TriangleWidth + edgeIndex;
            int triIndex = MarchingCubeTables.NativeTriangleTable[configIndexFlat];

            //No more triangles
            if (triIndex == -1)
                break;

            //float3 vertex1 = index + MarchingCubeTables.EdgeTable[triIndex, 0];
            //float3 vertex2 = index + MarchingCubeTables.EdgeTable[triIndex, 1];
            int edgeWidth = MarchingCubeTables.EdgeWidth;
            float3 vertex1 = index + MarchingCubeTables.NativeEdgeTable[triIndex * edgeWidth];
            float3 vertex2 = index + MarchingCubeTables.NativeEdgeTable[triIndex * edgeWidth + 1];

            //TODO Include smooth lerp
            float3 vertexPosition = math.lerp(vertex1, vertex2, 0.5f) * scaleFactor;

            vertices.Add(vertexPosition);
            triangles.Add(vertices.Count - 1);      
        }
        corners.Dispose();
    }
    public static void CreateMeshData(NativeArray<float> map, NativeList<int3> usedIndices, int resolution, float scaleFactor, ref Mesh mesh)
    {
        Profiler.BeginSample("MarchingCube");

        triangles.Clear();
        vertices.Clear();

       // NativeMultiHashMap<int, Triangle> triangleParralelList = new NativeMultiHashMap<int, Triangle>(resolution * resolution, Allocator.TempJob);
        NativeArray<CubeTriangles> cubeTriangles = new NativeArray<CubeTriangles>(usedIndices.Length, Allocator.TempJob);

        MarchCubeJob marchCubeJob = new MarchCubeJob()
        {
            indices = usedIndices,
            map = map,
            cornerTable = MarchingCubeTables.NativeCornerTable,
            triangleTable = MarchingCubeTables.NativeTriangleTable,
            edgeTable = MarchingCubeTables.NativeEdgeTable,
            scaleFactor = scaleFactor,
            resolution = resolution,
            cubeTriangles = cubeTriangles,
            //triangleVertices = triangleParralelList.AsParallelWriter()
        };
        marchCubeJob.Run(usedIndices.Length);


        for (int i = 0; i < cubeTriangles.Length; i++)
        {
            for (int j = 0; j < cubeTriangles[i].triCount; j++)
            {
                Triangle triangle = cubeTriangles[i].GetTriangle(j);
                vertices.Add(triangle.vertex1);
                triangles.Add(vertices.Count - 1);

                vertices.Add(triangle.vertex2);
                triangles.Add(vertices.Count - 1);

                vertices.Add(triangle.vertex3);
                triangles.Add(vertices.Count - 1);
            }
        }


        //var nativeVertices = triangleParralelList.GetValueArray(Allocator.Temp);
        //for (int i = 0; i < nativeVertices.Length; i++)
        //{
        //    vertices.Add(nativeVertices[i].vertex1);
        //    triangles.Add(vertices.Count - 1);

        //    vertices.Add(nativeVertices[i].vertex2);
        //    triangles.Add(vertices.Count - 1);

        //    vertices.Add(nativeVertices[i].vertex3);
        //    triangles.Add(vertices.Count - 1);
        //}

        //triangleParralelList.Dispose();
        cubeTriangles.Dispose();
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);

        mesh.RecalculateNormals();
        Profiler.EndSample();

    }
    [BurstCompile]
    public struct MarchCubeJob : IJobParallelFor
    {
        public NativeArray<int3> indices;
        [ReadOnly] public NativeArray<float> map;
        [ReadOnly] public NativeArray<int3> cornerTable;
        [ReadOnly] public NativeArray<int> triangleTable;
        [ReadOnly] public NativeArray<float3> edgeTable;

        public float scaleFactor;
        public int resolution;

        //public NativeMultiHashMap<int, Triangle>.ParallelWriter triangleVertices;
        public NativeArray<CubeTriangles> cubeTriangles;


        public void Execute(int threadIndex)
        {
            int3 index = indices[threadIndex];

            NativeArray<float> corners = new NativeArray<float>(8, Allocator.Temp);
            for (int j = 0; j < corners.Length; j++)
            {
                int3 corner = index + cornerTable[j];
                int cornerFlat = WaterMarchingCube.GetFlatIndex(corner, resolution);
                corners[j] = map[cornerFlat];
            }

            int configIndex = GetConfigIndex(corners);

            //empty config
            if (configIndex == 0 || configIndex == 255)
            {
                corners.Dispose();
                return;
            }

            CubeTriangles cubeTriangle = new CubeTriangles();
            int edgeIndex = 0;
            for (int i = 0; i < MAXEDGE; i++)
            {
                Triangle triangle = new Triangle();
                bool addTriangle = true;

                for (int j = 0; j < MAXTRI; j++)
                {
                    int triIndex = triangleTable[configIndex * MarchingCubeTables.TriangleWidth + edgeIndex];

                    ////No more triangles
                    if (triIndex == -1)
                    {
                        addTriangle = false;
                        break;
                    }

                    float3 vertex1 = index + edgeTable[triIndex * MarchingCubeTables.EdgeWidth];
                    float3 vertex2 = index + edgeTable[triIndex * MarchingCubeTables.EdgeWidth + 1];

                    //TODO Include smooth lerp
                    float3 vertexPosition = math.lerp(vertex1, vertex2, 0.5f) * scaleFactor;

                    //rofl
                    switch (j)
                    {
                        case 0:
                            triangle.vertex1 = vertexPosition;
                            break;
                        case 1:
                            triangle.vertex2 = vertexPosition;
                            break;
                        case 2:
                            triangle.vertex3 = vertexPosition;
                            break;
                    }
                    edgeIndex++;
                }
                if (addTriangle)
                {
                    cubeTriangle.AddTriangle(triangle);
                    //generate unique hash index
                    //const int separator = 4;
                    //int hashIndex = triangleVertices.m_ThreadIndex * separator + i;
                    //triangleVertices.Add(hashIndex, triangle);
                }
            }
            cubeTriangles[threadIndex] = cubeTriangle;
            corners.Dispose();
        }
    }

    public struct CubeTriangles
    {
        public int triCount;
        public Triangle triangle1;
        public Triangle triangle2;
        public Triangle triangle3;
        public Triangle triangle4;

        public void AddTriangle(Triangle triangle)
        {
            SetTriangle(triCount, triangle);
            triCount++;
        }

        public Triangle GetTriangle(int index)
        {
            switch (index)
            {
                case 0:
                    return triangle1;
                case 1:
                    return triangle2;
                case 2:
                    return triangle3;
                case 3:
                    return triangle4;
            }
            return triangle1;
        }

        public void SetTriangle(int index, Triangle triangle)
        {
            switch (index)
            {
                case 0:
                    triangle1 = triangle;
                    break;
                case 1:
                    triangle2 = triangle;
                    break;
                case 2:
                    triangle3 = triangle;
                    break;
                case 3:
                    triangle4 = triangle;
                    break;
            }
        }
    }

    public struct Triangle
    {
        public float3 vertex1;
        public float3 vertex2;
        public float3 vertex3;
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

    static int GetConfigIndex(NativeArray<int> corners)
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


    static int GetConfigIndex(NativeArray<float> corners)
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
