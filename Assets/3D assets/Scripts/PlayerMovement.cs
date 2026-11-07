using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// WASD to move, Space to jump, hold Shift to run (let go of Shift to walk again).
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;       // walking speed
    public float runSpeed = 8f;        // running speed (while Shift is held)
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    public float rotationSpeed = 12f;

    [Header("Facing")]
    [Tooltip("If the character's face doesn't point the way it moves, try 90, -90 or 180.")]
    public float modelYawOffset = 0f;
    [Tooltip("Stops animations from changing the character's size. Turn off only if you animate the scale on purpose.")]
    public bool lockScale = true;

    [Header("Animator Parameters (names must match the Animator window)")]
    public string walkingBool = "isWalking";   // Bool - true while a move key is held
    public string runningBool = "isRunning";   // Bool - true while moving in run mode
    public string jumpingBool = "isJumping";   // Bool - true from jump until landing
    public string jumpStateName = "Jump";      // Name of the jump STATE (the box in the Animator graph)

    // Set by PlayerBackstab while the attack animation plays
    [HideInInspector] public bool movementLocked;

    // True while Shift is held
    public bool IsRunning { get; private set; }

    // Kept only so TransformHUD still compiles.
    public bool IsBoosted => false;
    public float SpeedMultiplier => 1f;

    private CharacterController controller;
    private Animator animator;
    private Transform cam;
    private Vector3 velocity;
    private Vector3 baseScale;

    private float facingYaw;        // the direction we WANT to face (the script owns this, the Animator can't change it)
    private bool jumping;
    private float jumpStartTime;

    private readonly HashSet<string> animParams = new HashSet<string>();
    private readonly HashSet<string> warnedParams = new HashSet<string>();

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
        jumping = false;
    }

    // Used by PlayerBackstab to turn toward an enemy
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

        // Remember which parameters exist so missing ones don't spam the Console
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                animParams.Add(p.name);
    }

    void Update()
    {
        // Hold Shift to run. Let go and you are back to walking.
        IsRunning = RunHeld();

        Vector2 input = movementLocked ? Vector2.zero : ReadMoveInput();
        bool jumpPressed = !movementLocked && JumpPressed();

        // Camera-relative direction (uses only the camera's yaw)
        float yaw = cam != null ? cam.eulerAngles.y : 0f;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRot * Vector3.forward;
        Vector3 right = yawRot * Vector3.right;

        Vector3 moveDir = forward * input.y + right * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        bool isMoving = moveDir.sqrMagnitude > 0.001f;

        // Turn toward the direction of travel (applied in LateUpdate)
        if (isMoving)
        {
            float desiredYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float t = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);
            facingYaw = Mathf.LerpAngle(facingYaw, desiredYaw, t);
        }

        // Gravity and jump
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (jumpPressed && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumping = true;
            jumpStartTime = Time.time;
            PlayState(jumpStateName);   // start the jump animation directly
        }

        velocity.y += gravity * Time.deltaTime;

        float currentSpeed = IsRunning ? runSpeed : moveSpeed;
        Vector3 finalMove = moveDir * currentSpeed + Vector3.up * velocity.y;
        controller.Move(finalMove * Time.deltaTime);

        // The jump is over once we are back on the ground
        if (jumping && Time.time - jumpStartTime > 0.2f && controller.isGrounded)
            jumping = false;

        // Animator: these update instantly, so releasing the keys goes straight to idle
        SetBool(walkingBool, isMoving);
        SetBool(runningBool, isMoving && IsRunning);
        SetBool(jumpingBool, jumping);
    }

    // LateUpdate runs AFTER the Animator, so animations can't overwrite our facing or size
    void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(0f, facingYaw + modelYawOffset, 0f);
        if (lockScale) transform.localScale = baseScale;
    }

    // ---------------- Animator helpers ----------------

    // Plays an Animator trigger (used by PlayerBackstab). Warns once if it doesn't exist.
    public void PlayTrigger(string triggerName)
    {
        if (!HasParam(triggerName)) return;
        animator.SetTrigger(triggerName);
    }

    // Jumps straight into an Animator state by name, without needing a transition
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

    // ---------------- Input (works with the new Input System or the old Input Manager) ----------------

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