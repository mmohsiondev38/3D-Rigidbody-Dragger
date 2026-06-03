using UnityEngine;
using UnityEngine.InputSystem;

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

    [Tooltip("Maximum velocity (units/sec) allowed when dragging with physics")]
    public float maxDragVelocity = 10f;

    [Tooltip("Follow strength used to convert distance-to-target into desired velocity")]
    public float followSpeed = 8f;

    void Awake()
    {
        cam = Camera.main;

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
        if (cam == null) cam = Camera.main;
        Vector2 screenPos = pointerPosAction.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, draggableLayer))
        {
            grabbed = hit.transform;
            originalY = grabbed.position.y;

            // handle Rigidbody (make kinematic while dragging)
            grabbedRb = grabbed.GetComponent<Rigidbody>();
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
        if (cam == null) cam = Camera.main;
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
        // if grabbed has Rigidbody, physics movement handled in FixedUpdate
        if (grabbedHadRigidbody && grabbedRb != null) return;
        // smooth-move non-Rigidbody objects
        Vector3 newPos = Vector3.SmoothDamp(grabbed.position, targetPosition, ref currentVelocity, smoothTime);
        grabbed.position = newPos;
    }

    void FixedUpdate()
    {
        if (grabbed == null) return;
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
        grabbedHadRigidbody = false;
        currentVelocity = Vector3.zero;
    }
}
