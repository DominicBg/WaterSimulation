using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SinPosition : MonoBehaviour
{
    public float amplitude = 5;
    public float speed = 5;

    Vector3 startPos;
    Vector3 endPos;

    public void Start()
    {
        startPos = transform.position - transform.forward * amplitude;
        endPos = transform.position + transform.forward * amplitude;
    }

    private void Update()
    {
        float sin = Mathf.Sin(speed * Time.time);
        sin = (sin + 1) / 2;
        transform.position = Vector3.Lerp(startPos,endPos,sin);
    }
}
