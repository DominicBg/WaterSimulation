using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BoxObject : MonoCollision
{
    Box box;

    public bool toggleToUpdate;

    public override ICollision GetCollision()
    {
        return box;
    }

    public void OnValidate()
    {
        quaternion invrotation = math.inverse(transform.rotation);
        box = new Box()
        {
            min = -transform.localScale * 0.5f,
            max = transform.localScale * 0.5f,
            rotation = transform.rotation,
            invrotation = invrotation,
            position = transform.position
        };
    }
}

[System.Serializable]
public struct Box : ICollision
{
    public float3 position;
    public quaternion rotation;
    public quaternion invrotation;
    public float3 min;
    public float3 max;

    public bool TestCollision(float3 start, float3 end, out float ratio, out float3 normal)
    {
        start = math.mul(invrotation, position - start);
        end = math.mul(invrotation, position - end);

        float fmin = 0;
        float fmax = 1;
        ratio = 0;
        normal = 0;

        int faceId = 0;

        //Test one dimention at the time 
        if (!IsWithin(start.x, end.x, min.x, max.x, ref fmin, ref fmax, 1, ref faceId))
            return false;
        if (!IsWithin(start.y, end.y, min.y, max.y, ref fmin, ref fmax, 2, ref faceId))
            return false;
        if (!IsWithin(start.z, end.z, min.z, max.z, ref fmin, ref fmax, 3, ref faceId))
            return false;

        if (faceId > 0)
            ratio = fmin;
        else
            ratio = fmax;

        //Calculate the normal based on the faceId
        switch(math.abs(faceId))
        {
            case 1:
                normal = math.mul(rotation, new float3(1, 0, 0));
                break;
            case 2:
                normal = math.mul(rotation, new float3(0, 1, 0));
                break;
            case 3:
                normal = math.mul(rotation, new float3(0, 0, 1));
                break;
        }
        normal *= math.sign(faceId);
        return true;
    }

    private bool IsWithin(float start, float end, float min, float max, ref float fmin, ref float fmax, int id, ref int bestid)
    {
        float ffmin = (min - start) / (end - start);
        float ffmax = (max - start) / (end - start);

        //Swap
        if(ffmax < ffmin)
        {
            float temp = ffmin;
            ffmin = ffmax;
            ffmax = temp;
        }

        if (ffmax < fmin)
            return false;

        if (ffmin > fmax)
            return false;

        fmin = math.max(fmin, ffmin);
        fmax = math.min(fmax, ffmax);

        if (fmin > fmax)
            return false;

        //produced the closest intersection
        if (ffmin == fmin)
            bestid = id;
        else if (fmax == ffmax)
            bestid = -id;

        return true;
    }
}