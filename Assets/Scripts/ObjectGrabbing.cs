using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectGrabbing : MonoBehaviour
{
    [SerializeField] LayerMask grabMask;
    Camera playerCamera;
    [SerializeField] Transform grabTransform;
    [SerializeField] float grabRange;
    [SerializeField] float speedScale;
    Rigidbody grabbedObject;

    // Start is called before the first frame update
    void Start()
    {
        playerCamera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0) && grabbedObject == null)
        {
            Debug.Log("Clicked");
            Ray cameraRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
            if (Physics.Raycast(cameraRay, out RaycastHit hitInfo, grabRange, grabMask))
            {
                grabbedObject = hitInfo.rigidbody;
                grabbedObject.useGravity = false;
                Debug.Log("Grabbed");
            }
        }
        else if (Input.GetKeyUp(KeyCode.Mouse0) && grabbedObject != null)
        {
            grabbedObject.useGravity = true;
            grabbedObject = null;
            Debug.Log("Released");
        }
    }

    private void FixedUpdate()
    {
        if (grabbedObject)
        {
            Vector3 dir = grabTransform.position - grabbedObject.position;
            float dist = dir.magnitude;
            grabbedObject.velocity = dir.normalized * dist * speedScale;
        }
    }
}
