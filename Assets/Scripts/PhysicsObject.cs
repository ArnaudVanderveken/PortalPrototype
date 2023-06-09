using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsObject : PortalTraveller
{
    Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        base.Teleport(fromPortal, toPortal, pos, rot);
        rb.velocity = toPortal.TransformVector(fromPortal.InverseTransformVector(rb.velocity));
        rb.angularVelocity = toPortal.TransformVector(fromPortal.InverseTransformVector(rb.angularVelocity));
    }
}
