using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class ObjectDragger : MonoBehaviour
{
    [Tooltip("Layer mask used to raycast for draggable objects")]
    public LayerMask draggableLayer = ~0;

    Camera cam;

    InputAction pointerPosAction;
    InputAction pointerPressAction;

    Transform grabbed;
    Vector3 grabOffset;
    Plane dragPlane;

    [Tooltip("Height (world Y) to lift the object to when tapped")]
    public float liftHeight = 1f;

    float originalY;

    [Tooltip("When true, enable gravity on release so the object falls")]
    public bool dropOnRelease = false;

    [Tooltip("Smoothing time (seconds) for lift and drag movement")]
    public float smoothTime = 0.05f;

    Vector3 targetPosition;
    Vector3 currentVelocity;

    Rigidbody grabbedRb;
    bool grabbedHadRigidbody;
    bool originalUseGravity;
    bool originalIsKinematic;
    Draggable grabbedDraggable;

    [Tooltip("Maximum velocity (units/sec) allowed when dragging with physics")]
    public float maxDragVelocity = 10f;

    [Tooltip("Follow strength used to convert distance-to-target into desired velocity")]
    public float followSpeed = 8f;

    LevelManager levelManager;

    void Awake()
    {
        cam = Camera.main;
        levelManager = FindFirstObjectByType<LevelManager>();

        pointerPosAction = new InputAction("PointerPosition", InputActionType.PassThrough, "<Pointer>/position");
        pointerPosAction.performed += ctx => OnPointerMove(ctx.ReadValue<Vector2>());

        pointerPressAction = new InputAction("PointerPress", InputActionType.Button, "<Pointer>/press");
        pointerPressAction.performed += ctx => OnPointerDown();
        pointerPressAction.canceled += ctx => OnPointerUp();
    }

    void OnEnable()
    {
        pointerPosAction.Enable();
        pointerPressAction.Enable();
    }

    void OnDisable()
    {
        pointerPosAction.Disable();
        pointerPressAction.Disable();
    }

    void OnPointerDown()
    {
        Vector2 screenPos = pointerPosAction.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, draggableLayer))
        {
            Draggable draggable = hit.collider.GetComponentInParent<Draggable>();
            if (draggable != null && (draggable.snapped || draggable.snapping))
            {
                return;
            }

            grabbed = hit.collider.attachedRigidbody != null ? hit.collider.attachedRigidbody.transform : hit.transform;
            grabbedDraggable = draggable;
            originalY = grabbed.position.y;

            // handle Rigidbody (make kinematic while dragging)
            grabbedRb = hit.collider.attachedRigidbody != null ? hit.collider.attachedRigidbody : grabbed.GetComponent<Rigidbody>();
            grabbedHadRigidbody = grabbedRb != null;
            if (grabbedHadRigidbody)
            {
                originalIsKinematic = grabbedRb.isKinematic;
                originalUseGravity = grabbedRb.useGravity;
                // enable physics-driven movement (allow rotation via collisions)
                grabbedRb.isKinematic = false;
                grabbedRb.useGravity = false;
            }

            // set target to lifted position (smoothing will move it)
            targetPosition = new Vector3(grabbed.position.x, liftHeight, grabbed.position.z);

            // lock dragging to XZ plane at liftHeight
            dragPlane = new Plane(Vector3.up, new Vector3(0f, liftHeight, 0f));
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                grabOffset = targetPosition - worldPoint;
            }
        }
    }

    void OnPointerMove(Vector2 screenPos)
    {
        if (grabbed == null) return;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            // update smoothed target position (locked to liftHeight)
            targetPosition = worldPoint + grabOffset;
            targetPosition.y = liftHeight;
        }
    }

    void Update()
    {
        if (grabbed == null) return;
        if (TryStartSnap()) return;
        // if grabbed has Rigidbody, physics movement handled in FixedUpdate
        if (grabbedHadRigidbody && grabbedRb != null) return;
        // smooth-move non-Rigidbody objects
        Vector3 newPos = Vector3.SmoothDamp(grabbed.position, targetPosition, ref currentVelocity, smoothTime);
        grabbed.position = newPos;
    }

    void FixedUpdate()
    {
        if (grabbed == null) return;
        if (TryStartSnap()) return;
        if (!(grabbedHadRigidbody && grabbedRb != null)) return;

        // compute desired velocity towards targetPosition
        Vector3 toTarget = targetPosition - grabbedRb.position;

        // convert distance to a velocity using followSpeed
        Vector3 desiredVel = toTarget * followSpeed;

        // clamp magnitude to maxDragVelocity
        Vector3 clamped = Vector3.ClampMagnitude(desiredVel, maxDragVelocity);

        // ensure Y is driven toward liftHeight so object stays at plane while dragging
        float yTarget = liftHeight - grabbedRb.position.y;
        float yVel = Mathf.Clamp(yTarget * followSpeed, -maxDragVelocity, maxDragVelocity);
        clamped.y = yVel;

        // apply velocity - keep angular velocity untouched so rotations respond to collisions
        grabbedRb.linearVelocity = clamped;
    }

    void OnPointerUp()
    {
        if (grabbed == null) return;

        if (grabbedDraggable != null && grabbedDraggable.snapped)
        {
            grabbed = null;
            grabbedRb = null;
            grabbedDraggable = null;
            grabbedHadRigidbody = false;
            currentVelocity = Vector3.zero;
            return;
        }

        if (grabbedHadRigidbody && grabbedRb != null)
        {
            if (dropOnRelease)
            {
                grabbedRb.isKinematic = false;
                grabbedRb.useGravity = true;
            }
            else
            {
                // restore original rigidbody flags
                grabbedRb.isKinematic = originalIsKinematic;
                grabbedRb.useGravity = originalUseGravity;
            }
        }

        grabbed = null;
        grabbedRb = null;
        grabbedDraggable = null;
        grabbedHadRigidbody = false;
        currentVelocity = Vector3.zero;
    }

    bool TryStartSnap()
    {
        if (grabbed == null || grabbedDraggable == null || grabbedDraggable.snapped || grabbedDraggable.snapping) return false;
        if (grabbedDraggable.draggableTarget == null) return false;

        float distance = Vector3.Distance(grabbed.position, grabbedDraggable.draggableTarget.position);
        if (distance > grabbedDraggable.snapDistance) return false;

        Transform snapTarget = grabbedDraggable.draggableTarget;
        Draggable draggable = grabbedDraggable;
        Transform snapObject = grabbed;
        Rigidbody snapRb = grabbedRb;
        bool hadRigidbody = grabbedHadRigidbody;

        draggable.snapping = true;
        grabbed = null;
        grabbedRb = null;
        grabbedHadRigidbody = false;
        grabbedDraggable = null;
        currentVelocity = Vector3.zero;

        StartCoroutine(SnapRoutine(snapObject, snapRb, hadRigidbody, draggable, snapTarget));
        return true;
    }

    IEnumerator SnapRoutine(Transform snapObject, Rigidbody snapRb, bool hadRigidbody, Draggable draggable, Transform snapTarget)
    {
        if (snapObject == null || draggable == null || snapTarget == null)
        {
            if (draggable != null) draggable.snapping = false;
            yield break;
        }

        Vector3 startPosition = hadRigidbody && snapRb != null ? snapRb.position : snapObject.position;
        Quaternion startRotation = hadRigidbody && snapRb != null ? snapRb.rotation : snapObject.rotation;
        Vector3 startScale = snapObject.localScale;
        Vector3 targetPosition = snapTarget.position;
        Quaternion targetRotation = snapTarget.rotation;

        if (hadRigidbody && snapRb != null)
        {
            snapRb.linearVelocity = Vector3.zero;
            snapRb.angularVelocity = Vector3.zero;
            snapRb.isKinematic = true;
            snapRb.useGravity = false;
        }

        float lerpDuration = Mathf.Max(0.0001f, draggable.snapLerpDuration);
        float elapsed = 0f;

        while (elapsed < lerpDuration)
        {
            float t = Mathf.Clamp01(elapsed / lerpDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, t);
            Quaternion currentRotation = Quaternion.Slerp(startRotation, targetRotation, t);

            snapObject.SetPositionAndRotation(currentPosition, currentRotation);
            if (hadRigidbody && snapRb != null)
            {
                snapRb.position = currentPosition;
                snapRb.rotation = currentRotation;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        snapObject.SetPositionAndRotation(targetPosition, targetRotation);
        if (hadRigidbody && snapRb != null)
        {
            snapRb.position = targetPosition;
            snapRb.rotation = targetRotation;
        }
        Physics.SyncTransforms();

        snapTarget.gameObject.SetActive(false);

        float scaleDuration = Mathf.Max(0.0001f, draggable.snapScaleDuration);
        float halfDuration = scaleDuration * 0.5f;
        Vector3 pulseScale = startScale * draggable.snapScaleMultiplier;

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            float t = Mathf.Clamp01(elapsed / halfDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            snapObject.localScale = Vector3.Lerp(startScale, pulseScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            float t = Mathf.Clamp01(elapsed / halfDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            snapObject.localScale = Vector3.Lerp(pulseScale, startScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        snapObject.localScale = startScale;
        draggable.snapped = true;
        draggable.snapping = false;
        GetLevelManager()?.CheckLevelComplete();
        Physics.SyncTransforms();
    }

    LevelManager GetLevelManager()
    {
        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }

        return levelManager;
    }
}
