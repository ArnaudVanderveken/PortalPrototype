using System.Collections.Generic;
using UnityEngine;

public class Portal : MonoBehaviour
{
    [SerializeField] Portal linkedPortal;
    [SerializeField] MeshRenderer portalScreen;
    Vector3 startScreenLocalPos;
    Vector3 startScreenLocalScale;
    [SerializeField] float thickeningSize = 0.5f;
    Camera mainCamera;
    Camera portalCamera;
    RenderTexture renderTexture;
    List<PortalTraveller> trackedTravellers;

    [SerializeField] float nearClipOffset = 0.05f;
    [SerializeField] float nearClipLimit = 0.2f;

    private void Awake()
    {
        mainCamera = Camera.main;
        portalCamera = GetComponentInChildren<Camera>();
        portalCamera.enabled = false;
        trackedTravellers = new List<PortalTraveller>();
        startScreenLocalPos = portalScreen.transform.localPosition;
        startScreenLocalScale = portalScreen.transform.localScale;
    }

    private void LateUpdate()
    {
        for (int i = 0; i < trackedTravellers.Count; ++i)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerT = traveller.transform;
            
            Vector3 offsetFromPortal = travellerT.position - transform.position;
            int portalSide = System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
            int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));

            var mat = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerT.localToWorldMatrix;

            if (portalSide != portalSideOld)
            {
                var positionOld = travellerT.position;
                var rotOld = travellerT.rotation;
                traveller.Teleport(transform, linkedPortal.transform, mat.GetColumn(3), mat.rotation);
                traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotOld);

                linkedPortal.OnTravellerEnter(traveller);
                trackedTravellers.RemoveAt(i);
                if (traveller.gameObject.GetComponent<CharacterController>())
                    ResetPortal();
                --i;
            }
            else
            {
                traveller.graphicsClone.transform.SetPositionAndRotation(mat.GetColumn(3), mat.rotation);
                traveller.previousOffsetFromPortal = offsetFromPortal;
            }
                
        }
    }

    void CreateRenderTexture()
    {
        if (renderTexture == null || renderTexture.width != Screen.width || renderTexture.height != Screen.height)
        {
            // Release existing RT
            if (renderTexture != null)
                renderTexture.Release();

            // Create new RT
            renderTexture = new RenderTexture(Screen.width, Screen.height, 0);

            // Set RT as portal camera target
            portalCamera.targetTexture = renderTexture;

            // Set RT as linked portal material texture
            linkedPortal.portalScreen.material.SetTexture("_MainTex", renderTexture);
        }
    }

    void SetNearClipPlane()
    {
        // Learning resource:
        // http://www.terathon.com/lengyel/Lengyel-Oblique.pdf
        Transform clipPlane = transform;
        int dot = System.Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - portalCamera.transform.position));

        Vector3 camSpacePos = portalCamera.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
        Vector3 camSpaceNormal = portalCamera.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;

        // Don't use oblique clip plane if very close to portal as it seems this can cause some visual artifacts
        if (Mathf.Abs(camSpaceDst) > nearClipLimit)
        {
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);

            // Update projection based on new clip plane
            // Calculate matrix with player cam so that player camera settings (fov, etc) are used
            portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
        else
        {
            portalCamera.projectionMatrix = mainCamera.projectionMatrix;
        }
    }

    public static bool VisibleFromCamera(Renderer renderer, Camera camera)
    {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
    }

    public void Render()
    {
        if (!VisibleFromCamera(linkedPortal.portalScreen, mainCamera))
            return;

        portalScreen.enabled = false;

        // Check if RT is created
        CreateRenderTexture();

        // Move portalCamera to the correct position
        var mat = transform.localToWorldMatrix * linkedPortal.transform.worldToLocalMatrix * mainCamera.transform.localToWorldMatrix;
        portalCamera.transform.SetPositionAndRotation(mat.GetColumn(3), mat.rotation);

        // Update oblique near clipping plane with new transforms
        SetNearClipPlane();
        
        portalCamera.Render();

        portalScreen.enabled = true;
    }

    void UpdateSliceParams(PortalTraveller traveller)
    {
        // Calculate slice normal
        int side = SideOfPortal(traveller.transform.position);
        Vector3 sliceNormal = transform.forward * -side;
        Vector3 cloneSliceNormal = linkedPortal.transform.forward * side;

        // Calculate slice centre
        Vector3 slicePos = transform.position;
        Vector3 cloneSlicePos = linkedPortal.transform.position;

        // Apply parameters
        for (int i = 0; i < traveller.originalMaterials.Length; i++)
        {
            traveller.originalMaterials[i].SetVector("sliceCentre", slicePos);
            traveller.originalMaterials[i].SetVector("sliceNormal", sliceNormal);

            traveller.cloneMaterials[i].SetVector("sliceCentre", cloneSlicePos);
            traveller.cloneMaterials[i].SetVector("sliceNormal", cloneSliceNormal);
        }
    }

    void OnTravellerEnter(PortalTraveller traveller)
    {
        if (!trackedTravellers.Contains(traveller))
        {
            traveller.EnterPortalThreshold();
            traveller.previousOffsetFromPortal = traveller.transform.position - transform.position;
            trackedTravellers.Add(traveller);
            if (traveller.gameObject.GetComponent<CharacterController>())
                ThickenPortal();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if (traveller != null)
            OnTravellerEnter(traveller);
    }

    private void OnTriggerExit(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if (traveller != null && trackedTravellers.Contains(traveller))
        {
            traveller.ExitPortalThreshold();
            trackedTravellers.Remove(traveller);
            if (traveller.gameObject.GetComponent<CharacterController>())
                ResetPortal();
        }
    }

    public void PrePortalRender()
    {
        foreach (var traveller in trackedTravellers)
            UpdateSliceParams(traveller);
    }

    public void PostPortalRender()
    {
        foreach (var traveller in trackedTravellers)
            UpdateSliceParams(traveller);
    }

    void ThickenPortal()
    {
        portalScreen.transform.localPosition -= System.Math.Sign(Vector3.Dot(transform.forward, mainCamera.transform.position - transform.position)) * transform.forward * thickeningSize / 2;
        var currentScale = portalScreen.transform.localScale;
        portalScreen.transform.localScale = new Vector3(currentScale.x, currentScale.y, thickeningSize);
    }

    void ResetPortal()
    {
        portalScreen.transform.localPosition = startScreenLocalPos;
        portalScreen.transform.localScale = startScreenLocalScale;
    }

    /* HELPERS*/

    int SideOfPortal(Vector3 pos)
    {
        return System.Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
    }

    bool SameSideOfPortal(Vector3 posA, Vector3 posB)
    {
        return SideOfPortal(posA) == SideOfPortal(posB);
    }
}
