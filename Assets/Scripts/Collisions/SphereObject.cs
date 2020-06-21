using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public class SphereObject : MonoCollision
{
    public Sphere sphere;
    public float radius;
    public float thickness;

    private void OnValidate()
    { 
        sphere.position = transform.position;
        sphere.radius = radius;
        sphere.thickness = thickness;
        transform.localScale = Vector3.one * radius * 2f;
    }

    private void OnDrawGizmos()
    {
        OnValidate();
        Gizmos.DrawSphere(sphere.position, sphere.radius);
        Gizmos.DrawSphere(sphere.position, sphere.radius - sphere.thickness);
    }

    public override ICollision GetCollision()
    {
        return sphere;
    }
}

public struct Sphere : ICollision
{
    public float3 position;
    public float radius;
    public float thickness;

    [BurstCompile]
    public bool TestCollision(float3 startPosition, float3 endPosition, out float ratio, out float3 normal)
    {
        ratio = 0;
        normal = 0;

        float radiusThickness = (radius - thickness);
        float radiusThicknessSq = radiusThickness * radiusThickness;

        //is inside
        float startPosDistSq = math.distancesq(position, startPosition);
        bool startsInside = startPosDistSq < radiusThicknessSq;

        //is outside
        float endPosDistSq = math.distancesq(position, endPosition);
        bool endsInside = endPosDistSq < radiusThicknessSq;

        //They are the same
        if (startsInside == endsInside)
            return false;

        ////implies didnt start inside
        //if(startsInside)
        //{

        //    return 
        //}

        //outside to inside
        if (!startsInside)
        {
            float3 rd = endPosition - startPosition;
            float t = math.dot(position - startPosition, rd);
            float3 p = startPosition + rd * t;
            float y = math.length(position - p);
            float x = math.sqrt(radius * radius - y * y);

            float3 hitPos = startPosition + rd * x;
            ratio = x / radius; //precalcualte inv radius
            normal = math.normalize(hitPos - position);
            return true;
        }

        return false;
    }
}