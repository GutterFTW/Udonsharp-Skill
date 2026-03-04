using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// Rotates the attached GameObject continuously in the world.
/// Demonstrates: Update, Time.deltaTime, no networking needed.
/// </summary>
public class RotatingCube : UdonSharpBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees per second around each axis.")]
    public float rotationSpeedX = 0f;
    public float rotationSpeedY = 90f;
    public float rotationSpeedZ = 0f;

    private void Update()
    {
        transform.Rotate(
            rotationSpeedX * Time.deltaTime,
            rotationSpeedY * Time.deltaTime,
            rotationSpeedZ * Time.deltaTime
        );
    }
}
