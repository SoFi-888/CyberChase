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
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    public string walkingBool = "isWalking";
    public string jumpTrigger = "Jump";

    [HideInInspector] public bool movementLocked;

    public bool IsBoosted => false;
    public float SpeedMultiplier => 1f;

    private CharacterController controller;
    private Animator animator;
    private Transform cam;
    private Vector3 velocity;

    private readonly HashSet<string> animParams = new HashSet<string>();
    private readonly HashSet<string> warnedParams = new HashSet<string>();

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        if (Camera.main != null) cam = Camera.main.transform;

        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                animParams.Add(p.name);
    }

    void Update()
    {
        Vector2 input = movementLocked ? Vector2.zero : ReadMoveInput();
        bool jumpPressed = !movementLocked && JumpPressed();

        float yaw = cam != null ? cam.eulerAngles.y : 0f;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = yawRot * Vector3.forward;
        Vector3 right = yawRot * Vector3.right;

        Vector3 moveDir = forward * input.y + right * input.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        bool isMoving = moveDir.sqrMagnitude > 0.001f;

        // Walk animation
        if (animator != null && animParams.Contains(walkingBool))
            animator.SetBool(walkingBool, isMoving && controller.isGrounded);

        // Gravity and jump
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (jumpPressed && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            PlayTrigger(jumpTrigger);   // jump animation
        }

        velocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = moveDir * moveSpeed + Vector3.up * velocity.y;
        controller.Move(finalMove * Time.deltaTime);
    }
    public void PlayTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName)) return;

        if (!animParams.Contains(triggerName))
        {
            if (warnedParams.Add(triggerName))
                Debug.LogWarning("PlayerMovement: Animator has no parameter named '" + triggerName +
                                 "'. Add a Trigger with that name in the Animator window.");
            return;
        }
        animator.SetTrigger(triggerName);
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
}