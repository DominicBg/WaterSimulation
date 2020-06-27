using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class SPHSystem2D : MonoBehaviour
{
    public int2 count2 = 10;
    public float2 scale = 1;
    public float2 startPos = new float2(0, 5);
    int count;
    WaterParticle2D[] particles = new WaterParticle2D[0];
    private void Start()
    {
        Restart();
    }

    void Restart()
    {
        count = count2.x * count2.y;
        particles = new WaterParticle2D[count];
        int i = 0;
        for (int x = 0; x < count2.x; x++)
        {
            for (int y = 0; y < count2.x; y++)
            {
                particles[i++].x = new float2(startPos.x + x * scale.x, startPos.y + y * scale.y);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            Restart();
        }

        CalculateConst();
        ComputeDensityPressure();
        ComputeForce();
        Integrate();

        CalculateStats();
    }

    void CalculateStats()
    {
        float density = 0;
        float pressure = 0;

        float invLength = 1f / particles.Length;

        for (int i = 0; i < particles.Length; i++)
        {
            density += particles[i].rho * invLength;
            pressure += particles[i].p * invLength;
        }

        Debug.Log($"Mean Density {density}, mean pressure {pressure}");
    }

    void ComputeDensityPressure()
    {
        for (int i = 0; i < count; i++)
        {
            WaterParticle2D pi = particles[i];
            pi.rho = 0;
            for (int j = 0; j < count; j++)
            {
                WaterParticle2D pj = particles[j];
                float2 rij = pj.x - pi.x;
                float r2 = math.lengthsq(rij);
                if(r2 < HSQ)
                {
                    pi.rho += MASS * POLY6 * math.pow(HSQ - r2, 3);
                }

            }
            //pi.p = GAS_CONST2 * (math.pow(pi.rho / REST_DENS, 7) - 1);
            pi.p = GAS_CONST * (pi.rho - REST_DENS);
            particles[i] = pi;
        }
    }

    void ComputeForce()
    {
        for (int i = 0; i < count; i++)
        {
            float2 fpress = 0;
            float2 fvisc = 0;

            WaterParticle2D pi = particles[i];
            for (int j = 0; j < count; j++)
            {
                WaterParticle2D pj = particles[j];
                if (i == j)
                    continue;

                float2 rij = pj.x - pi.x;
                float r = math.length(rij);
                if(r < H)
                {
                    fpress += -math.normalize(rij) * MASS * (pi.p + pj.p) / (2f * pj.rho) * SPIKY_GRAD * math.pow(H - r, 2);
                    fvisc += VISC * MASS * (pj.v - pi.v) / pj.rho * VISC_LAP * (H - r);
                }
            }

            float2 fgrav = G * pi.rho;
            pi.f = fpress + fvisc + fgrav;
            particles[i] = pi;
        }
    }

    void Integrate()
    {
        for (int i = 0; i < count; i++)
        {    
            WaterParticle2D p = particles[i];
            p.v += Time.deltaTime * p.f / p.rho;
            p.x += Time.deltaTime * p.v;

            if (p.x.x - EPS < 0.0f)
            {
                p.v.x *= BOUND_DAMPING;
                p.x.x = EPS;
            }
            if (p.x.x + EPS > VIEW_WIDTH)
            {
                p.v.x *= BOUND_DAMPING;
                p.x.x = VIEW_WIDTH - EPS;
            }
            if (p.x.y - EPS < 0.0f)
            {
                p.v.y *= BOUND_DAMPING;
                p.x.y = EPS;
            }
            if (p.x.y + EPS > VIEW_HEIGHT)
            {
                p.v.y *= BOUND_DAMPING;
                p.x.y = VIEW_HEIGHT - EPS;
            }
            particles[i] = p;
        }
    }

    public float2 G = new float2(0, -9.81f);
    public float H = 16;
    public float MASS = 65;
    public float BOUND_DAMPING = -0.5f;
    public float REST_DENS = 1000;
    public float GAS_CONST = 2000;
    public float VISC = 250;
    public float VIEW_HEIGHT = 255;
    public float VIEW_WIDTH = 255;
    public float DT = 0.0008f;
    public float GAS_CONST2 = 1.5f;

    float VISC_LAP;
    float POLY6;
    float SPIKY_GRAD;
    float HSQ;
    float EPS;

    private void OnDrawGizmos()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            float2 pos = particles[i].x;
            Gizmos.DrawSphere(new float3(pos.x, pos.y, 0), H);
        }
    }

    void CalculateConst()
    {
        POLY6 = 315f / (65f * math.PI* math.pow(H, 9f));
        HSQ = H * H;
        EPS = H;
        SPIKY_GRAD = -45f / (math.PI * math.pow(H, 6f));
        VISC_LAP = 45f / (math.PI * math.pow(H, 6f));
    }


    public struct WaterParticle2D
    {
        public float2 x;
        public float2 v;
        public float2 f;
        public float rho;
        public float p;
    }
}
