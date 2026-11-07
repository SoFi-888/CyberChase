using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterController))]
public class SpawnAtCheckpoint : MonoBehaviour
{
    [Header("Leave empty to auto-find an object named 'Checkpoint'")]
    public Transform checkpoint;
    public string checkpointName = "Checkpoint";

    [Header("Spawn")]
    public float heightOffset = 0.1f;
    public float respawnCooldown = 0.5f;

    private CharacterController controller;
    private PlayerMovement movement;
    private Vector3 spawnPoint;
    private bool hasSpawnPoint;
    private float lastRespawnTime = -10f;

    public Bounds GetBounds()
    {
        return controller.bounds;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();

        if (checkpoint == null)
        {
            GameObject go = GameObject.Find(checkpointName);
            if (go != null) checkpoint = go.transform;
        }

        if (checkpoint == null)
        {
            Debug.LogWarning("SpawnAtCheckpoint: no object named '" + checkpointName + "' found in this scene.");
            return;
        }

        SetCheckpoint(checkpoint);
        TeleportTo(spawnPoint);
    }

    public void SetCheckpoint(Transform cp)
    {
        Vector3 pos = cp.position;
        Collider col = cp.GetComponent<Collider>();
        if (col != null) pos.y = col.bounds.max.y;
        pos.y += heightOffset;

        spawnPoint = pos;
        hasSpawnPoint = true;
    }

    public void Respawn()
    {
        if (!hasSpawnPoint) return;
        if (Time.time - lastRespawnTime < respawnCooldown) return;
        lastRespawnTime = Time.time;
        TeleportTo(spawnPoint);
    }

    void TeleportTo(Vector3 pos)
    {
        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;

        if (movement != null) movement.ResetVelocity();
    }
}