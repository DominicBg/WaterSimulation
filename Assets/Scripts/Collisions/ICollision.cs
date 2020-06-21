using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public interface ICollision
{
    bool TestCollision(float3 startPosition, float3 endPosition, out float ratio, out float3 normal);
}
