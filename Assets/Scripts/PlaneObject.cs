using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlaneObject : MonoBehaviour
{
    public float2 size;
    public Plane plane;


#if UNITY_EDITOR
    public void OnValidate()
    {
        plane = new Plane(transform.forward, size, transform.position);
    }
#endif


}

[System.Serializable]
public struct Plane
{
    public float3 normal;
    public float2 size;
    public float3 position;
    private float distance;

    public Plane(float3 normal, float2 size, float3 position)
    {
        this.normal = normal;
        this.size = size;
        this.position = position;
        distance = -math.dot(normal, position);
    }

    public float GetDistanceToPoint(float3 point)
    {
        return math.dot(normal, point) + distance;
    }

    public bool Raycast(float3 start, float3 end, out float enter)
    {
        float distanceToPoint = GetDistanceToPoint(end);
        if (distanceToPoint * distanceToPoint > math.distancesq(start, end))
        {
            enter = 0f;
            return false;
        }

        float3 direction = end - start;
        float dotDirNormal = math.dot(direction, normal);
        float num2 = -math.dot(start, normal) - distance;
        if (dotDirNormal < math.FLT_MIN_NORMAL)
        {
            enter = 0f;
            return false;
        }
        enter = num2 / dotDirNormal;
        return enter > 0f;
    }
}

#if UNITY_EDITOR
public class PlaneObjectGizmo
{
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
    static void RenderLightGizmo(GameObject obj, GizmoType gizmoType)
    {
        PlaneObject planeObject = obj.GetComponent<PlaneObject>();
        if (planeObject != null)
        {

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(obj.transform.position, obj.transform.forward);

            Vector3[] points = new Vector3[4];
            Plane plane = planeObject.plane;
            points[0] = drawCubeAndReturnPosition(ref obj, new Vector3(plane.size.x, plane.size.y));
            points[1] = drawCubeAndReturnPosition(ref obj, new Vector3(plane.size.x, -plane.size.y));
            points[2] = drawCubeAndReturnPosition(ref obj, new Vector3(-plane.size.x, -plane.size.y));
            points[3] = drawCubeAndReturnPosition(ref obj, new Vector3(-plane.size.x, plane.size.y));

            Gizmos.DrawLine(points[0], points[1]);
            Gizmos.DrawLine(points[0], points[3]);
            Gizmos.DrawLine(points[1], points[2]);
            Gizmos.DrawLine(points[2], points[3]);
            planeObject.OnValidate();
        }
    }

    private static Vector3 drawCubeAndReturnPosition(ref GameObject obj, Vector3 renderOffset)
    {
        Vector3 cubeSize = new Vector3(0.05f, 0.05f, 0.05f);
        Vector3 calculatedOffsetPosition = obj.transform.position + obj.transform.rotation * renderOffset;
        Gizmos.DrawCube(calculatedOffsetPosition, cubeSize);
        return calculatedOffsetPosition;
    }
}
#endif
