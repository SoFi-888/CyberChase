using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    public float rotationSpeed = 12f;
    [Tooltip("Seconds after leaving the ground that you can still jump. Makes jumping off moving platforms and ledges feel fair. Set to 0 to turn off.")]
    public float coyoteTime = 0.1f;

    [Header("Facing")]
    [Tooltip("If the character's face doesn't point the way it moves, try 90, -90 or 180.")]
    public float modelYawOffset = 0f;
    public bool lockScale = true;

    [Header("Animator Parameters (names must match the Animator window)")]
    public string walkingBool = "isWalking";
    public string runningBool = "isRunning";
    public string jumpingBool = "isJumping";
    public string jumpStateName = "Jump";

    // Set by PlayerBackstab while the attack animation plays
    [HideInInspector] public bool movementLocked;

    public bool IsRunning { get; private set; }

    public bool IsGrounded { get { return grounded; } }

    public bool RecentlyGrounded
    {
        get { return Time.time - lastGroundedTime <= Mathf.Max(coyoteTime, 0.1f); }
    }

    public bool IsBoosted => false;
    public float SpeedMultiplier => 1f;

    private CharacterController controller;
    private Animator animator;
    private Transform cam;
    private Vector3 velocity;
    private Vector3 baseScale;

    private float facingYaw;
    private bool jumping;
    private bool grounded;
    private float lastGroundedTime = -100f;
    private float jumpStartTime;

    private readonly HashSet<string> animParams = new HashSet<string>();
    private readonly HashSet<string> warnedParams = new HashSet<string>();

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
        jumping = false;
    }

    public void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        facingYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        if (Camera.main != null) cam = Camera.main.transform;

        baseScale = transform.localScale;
        facingYaw = transform.eulerAngles.y - modelYawOffset;

        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                animParams.Add(p.name);
    }

    void Update()
    {
        IsRunning = RunHeld();

        Vector2 input = movementLocked ? Vector2.zero : ReadMoveInput();
        bool jumpPressed = !movementLocked && JumpPressed();

        float yaw = cam != null ? cam.eulerAngles.y : 0f;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRot * Vector3.forward;
        Vector3 right = yawRot * Vector3.right;

        Vector3 moveDir = forward * input.y + right * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        bool isMoving = moveDir.sqrMagnitude > 0.001f;

        if (isMoving)
        {
            float desiredYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float t = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);
            facingYaw = Mathf.LerpAngle(facingYaw, desiredYaw, t);
        }

        if (grounded) lastGroundedTime = Time.time;

        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        if (jumpPressed && canJump)
        {
            lastGroundedTime = -100f;
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumping = true;
            jumpStartTime = Time.time;
            PlayState(jumpStateName);
        }

        velocity.y += gravity * Time.deltaTime;

        float currentSpeed = IsRunning ? runSpeed : moveSpeed;
        Vector3 finalMove = moveDir * currentSpeed + Vector3.up * velocity.y;
        controller.Move(finalMove * Time.deltaTime);
        grounded = controller.isGrounded;

        if (jumping && Time.time - jumpStartTime > 0.2f && grounded)
            jumping = false;

        SetBool(walkingBool, isMoving);
        SetBool(runningBool, isMoving && IsRunning);
        SetBool(jumpingBool, jumping);
    }

    void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(0f, facingYaw + modelYawOffset, 0f);
        if (lockScale) transform.localScale = baseScale;
    }

    public void PlayTrigger(string triggerName)
    {
        if (!HasParam(triggerName)) return;
        animator.SetTrigger(triggerName);
    }

    void PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash))
        {
            if (warnedParams.Add("state:" + stateName))
                Debug.LogWarning("PlayerMovement: there is no Animator state named '" + stateName +
                                 "'. Rename your jump state (the box in the Animator graph) to exactly that.");
            return;
        }
        animator.CrossFadeInFixedTime(hash, 0.05f);
    }

    void SetBool(string boolName, bool value)
    {
        if (!HasParam(boolName)) return;
        animator.SetBool(boolName, value);
    }

    bool HasParam(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        if (animParams.Contains(paramName)) return true;

        if (warnedParams.Add(paramName))
            Debug.LogWarning("PlayerMovement: Animator has no parameter named '" + paramName +
                             "'. Add one with that exact name in the Animator window.");
        return false;
    }

    Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    bool JumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    bool RunHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }
}