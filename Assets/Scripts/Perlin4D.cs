using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class Perlin4D : MonoBehaviour
{
    public int resolution = 25;
    public float scale = 1;
    public float thresHold = .5f;

    float[,,] map;


    public float speed = 1;
    public float4 position;

    Mesh mesh;
    public MeshFilter filter;
    // Start is called before the first frame update
    void Start()
    {
        map = new float[resolution, resolution, resolution];
        mesh = new Mesh();
    }

    // Update is called once per frame
    void Update()
    {
        Inputs();
        GenerateMap();
    }

    private void Inputs()
    {
        if (Input.GetKey(KeyCode.Z))
            position.w += speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.X))
            position.w -= speed * Time.deltaTime;
    }

    void GenerateMap()
    {
        int resolutionCube = resolution * resolution * resolution;
        if(map.Length != resolutionCube)
        {
            map = new float[resolution, resolution, resolution];
        }

        NativeList<int3> usedIndices = new NativeList<int3>(Allocator.Temp);
        for (int x = 0; x < resolution-1; x++)
        {
            for (int y = 0; y < resolution-1; y++)
            {
                for (int z = 0; z < resolution-1; z++)
                {
                    float4 pos = new float4(position.x + x * scale, position.y + y * scale, position.z + z * scale, position.w);
                    float perlin = noise.cnoise(pos);
                    map[x, y, z] = perlin > thresHold ? 1 : 0;
                    usedIndices.Add(new int3(x, y, z));
                }
            }
        }

        MarchingCube.CreateMeshData(map, usedIndices, scale, ref mesh);
        usedIndices.Dispose();

        filter.mesh = mesh;
    }
}
