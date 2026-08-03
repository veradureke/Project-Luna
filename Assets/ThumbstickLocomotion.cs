using UnityEngine;
using UnityEngine.InputSystem;

public class ThumbstickLocomotion : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 2.0f;
    public float gravity = -9.81f;
    [Tooltip("Max downward speed (m/s). Caps the fall so a legitimate drop can never " +
             "outrun the CharacterController's collision sweep against the thin ground plane.")]
    public float maxFallSpeed = 10f;
    [Tooltip("How far below the feet to look for a floor before applying gravity. If no " +
             "collider is found within this range the player hovers in place instead of " +
             "free-falling — e.g. while the HubScene ground is still streaming in.")]
    public float groundCheckDistance = 100f;
    public Transform headTransform; // Assign CenterEyeAnchor for gaze-oriented movement

    [Header("Turning Settings")]
    public bool enableSnapTurn = true;
    public float snapTurnAngle = 45f;
    public float snapTurnCooldown = 0.5f;

    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction lookAction;
    private float verticalVelocity;
    private float lastTurnTime;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            
            // Standard VR CharacterController dimensions
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0, 0.9f, 0);
            characterController.stepOffset = 0.3f;
        }

        // Attempt to auto-find the CenterEyeAnchor under OVRCameraRig
        if (headTransform == null)
        {
            headTransform = transform.Find("TrackingSpace/CenterEyeAnchor");
        }

        // Initialize and enable input actions from the project-wide action maps
        if (InputSystem.actions != null)
        {
            var playerMap = InputSystem.actions.FindActionMap("Player");
            if (playerMap != null && !playerMap.enabled)
            {
                playerMap.Enable();
            }

            moveAction = InputSystem.actions.FindAction("Player/Move");
            lookAction = InputSystem.actions.FindAction("Player/Look");
        }
    }

    void Update()
    {
        // 1. Handle Locomotion (Left Thumbstick / WASD)
        Vector2 moveInput = Vector2.zero;
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }

        // NOTE: The LEFT controller thumbstick is intentionally DISABLED for locomotion.
        // This block previously read the LEFT hand's raw primary2DAxis (left stick) as a
        // fallback and fed it into movement; it has been removed. Locomotion now comes only
        // from the Player/Move action, whose XR binding is scoped to the RIGHT controller
        // (<XRController>{RightHand}/{Primary2DAxis}) plus keyboard/gamepad for editor use.
        // Snap-turn (right stick, handled below) and all grab/laser/button input are unchanged.

        // Calculate move direction projected onto the horizontal plane relative to head orientation
        Vector3 moveDirection = Vector3.zero;
        if (headTransform != null)
        {
            Vector3 forward = headTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = headTransform.right;
            right.y = 0f;
            right.Normalize();

            moveDirection = (forward * moveInput.y + right * moveInput.x);
        }
        else
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            moveDirection = (forward * moveInput.y + right * moveInput.x);
        }

        Vector3 move = moveDirection * speed * Time.deltaTime;

        // Apply gravity — but only when there is actually a floor beneath the player.
        // The rig lives in the Persistent scene and is active BEFORE the ground
        // (HubScene) streams in — and on the first selection a spoken welcome plays in
        // that gap — so for a stretch there is no collider below at all. Accumulating
        // gravity during that window free-falls the player into the void; the ground
        // plane is a single-sided, zero-thickness collider, so once it finally loads the
        // player is already below it and keeps going ("falling through the ground").
        // Guard: if nothing is below, hover in place; otherwise fall normally, capped to
        // a terminal speed as a safety net.
        if (characterController.isGrounded)
        {
            verticalVelocity = -0.5f; // small downward force to keep grounded
        }
        else if (!HasGroundBelow())
        {
            verticalVelocity = 0f;    // no floor loaded yet — don't sink into the void
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            if (verticalVelocity < -maxFallSpeed) verticalVelocity = -maxFallSpeed;
        }
        move.y = verticalVelocity * Time.deltaTime;

        characterController.Move(move);

        // Dynamic collider height to match physical player head height
        UpdateColliderHeight();

        // 2. Handle Snap Turning (Right Thumbstick / Gamepad Right Stick)
        if (enableSnapTurn && Time.time > lastTurnTime + snapTurnCooldown)
        {
            Vector2 lookInput = Vector2.zero;
            if (lookAction != null)
            {
                lookInput = lookAction.ReadValue<Vector2>();
            }

            if (lookInput == Vector2.zero)
            {
                UnityEngine.XR.InputDevice rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
                if (rightHand.isValid)
                {
                    rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out lookInput);
                }
            }

            if (Mathf.Abs(lookInput.x) > 0.5f)
            {
                float angle = Mathf.Sign(lookInput.x) * snapTurnAngle;
                transform.Rotate(Vector3.up, angle);
                lastTurnTime = Time.time;
            }
        }
    }

    // True if there is any solid collider beneath the player within
    // groundCheckDistance. Used to suppress gravity while no floor is loaded yet (the
    // Persistent rig precedes / outlives the HubScene ground), so the player can't
    // free-fall into the void and drop past the ground plane when it finally streams in.
    bool HasGroundBelow()
    {
        // Start just below the capsule's feet so the ray doesn't hit the
        // CharacterController's own collider.
        Vector3 feet = transform.TransformPoint(characterController.center)
                       + Vector3.down * (characterController.height * 0.5f
                                         + characterController.skinWidth + 0.02f);
        return Physics.Raycast(feet, Vector3.down, groundCheckDistance,
            ~0, QueryTriggerInteraction.Ignore);
    }

    void UpdateColliderHeight()
    {
        if (headTransform != null)
        {
            // Update capsule height and center based on local head height
            float headHeight = Mathf.Clamp(headTransform.localPosition.y, 0.5f, 2.5f);
            characterController.height = headHeight;
            
            Vector3 center = characterController.center;
            center.y = headHeight / 2f + characterController.skinWidth;
            center.x = headTransform.localPosition.x;
            center.z = headTransform.localPosition.z;
            characterController.center = center;
        }
    }
}
