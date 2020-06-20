using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class SPHMarchingCubeRenderer : MonoBehaviour
{
    public MeshFilter waterMeshFilter;
    public BoxCollider cubeMarchineZone;
    public int marchingCubeResolution;
    Mesh mesh;

    private void Start()
    {
        mesh = new Mesh();
    }

    public void RenderWater(NativeArray<SPHSystem.WaterParticle> waterParticles)
    {
        float3 minPosition = cubeMarchineZone.bounds.min;
        float3 maxPosition = cubeMarchineZone.bounds.max;

        WaterMarchingCube.GenerateMesh(waterParticles, waterParticles.Length, minPosition, maxPosition, marchingCubeResolution, ref mesh);
        waterMeshFilter.mesh = mesh;
        waterMeshFilter.transform.localScale = Vector3.one * cubeMarchineZone.size.x;
        waterMeshFilter.transform.localPosition = -Vector3.one * cubeMarchineZone.size.x * 0.5f;
    }
}
