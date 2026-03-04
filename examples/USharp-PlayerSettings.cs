using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Applies custom locomotion settings to the local player when they join.
/// Demonstrates: Start(), VRCPlayerApi, locomotion API, IsValid().
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerSettings : UdonSharpBehaviour
{
    [Header("Locomotion")]
    [Tooltip("Walking speed. Default is 2.")]
    public float walkSpeed = 2f;

    [Tooltip("Running speed. Default is 4.")]
    public float runSpeed = 4f;

    [Tooltip("Strafe speed. Default is 2.")]
    public float strafeSpeed = 2f;

    [Tooltip("Jump impulse. Default is 0 (no jump).")]
    public float jumpImpulse = 3f;

    [Tooltip("Gravity multiplier. Default is 1 (Earth).")]
    public float gravityStrength = 1f;

    [Header("Voice")]
    [Tooltip("Voice gain in dB. Range 0-24, default 15.")]
    public float voiceGain = 15f;

    [Tooltip("Max voice distance in meters. Default 25.")]
    public float voiceFarDistance = 25f;

    private void Start()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local)) return;

        local.SetWalkSpeed(walkSpeed);
        local.SetRunSpeed(runSpeed);
        local.SetStrafeSpeed(strafeSpeed);
        local.SetJumpImpulse(jumpImpulse);
        local.SetGravityStrength(gravityStrength);

        local.SetVoiceGain(voiceGain);
        local.SetVoiceDistanceNear(0f); // keep near at 0 for proper spatialization
        local.SetVoiceDistanceFar(voiceFarDistance);
    }
}
