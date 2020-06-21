using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MonoCollision : MonoBehaviour
{
    public abstract ICollision GetCollision();
}
