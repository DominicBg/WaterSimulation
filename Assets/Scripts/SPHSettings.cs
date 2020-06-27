using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "SPHSettings", menuName ="SPHSettings")]
public class SPHSettings : ScriptableObject
{
    public float particleRadius;
    public float3 gravity = new float3(0f, -9.81f, 0);
    public float restDensity = 1f;  // rest density
    public float gasConstant = 2f;// const for equation of state
    public float mass = .65f; // assume all particles have the same mass
    public float viscosity = .250f;
    public float collisionElasticity = .5f;
    public float POLY6 ()
    {
        return 315f / (64f * math.PI* math.pow(particleRadius, 9f));      
    }
    public float CalculateSpikyGrad()
    {
       // return -45f / (math.PI * math.pow(particleRadius, 6f));
        return 15f / (math.PI * math.pow(particleRadius, 6f));
    }

    public float CalculateViscosityKernel()
    {
        return 45f / (math.PI * math.pow(particleRadius, 6f));
        //return 15 / (2*math.PI * math.pow(particleRadius, 3f));
    }
}
