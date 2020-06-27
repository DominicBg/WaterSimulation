using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class InnerBoxObject : MonoCollision
{
    public override ICollision GetCollision()
    {
        return new InnerBox()
        {
            position = transform.position,
            size = transform.lossyScale / 2
        };
    }
}

public struct InnerBox : ICollision
{
    public float3 position;
    public float3 size;

    public bool TestCollision(float3 startPosition, float3 endPosition, out float ratio, out float3 normal)
    {
        ratio = 0;

        if(endPosition.x > position.x + size.x)
        {
            normal = new float3(-1, 0, 0);
            return true;       
        }
        if (endPosition.x < position.x - size.x)
        {
            normal = new float3(1, 0, 0);
            return true;
        }
        if (endPosition.z > position.z + size.z)
        {
            normal = new float3(0, 0, -1);
            return true;
        }
        if (endPosition.z < position.z - size.z)
        {
            normal = new float3(0, 0, 1);
            return true;
        }
        if (endPosition.y < position.y - size.y)
        {
            normal = new float3(0, 1, 0);
            return true;
        }
        normal = 0;
        return false;
    }


}