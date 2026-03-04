using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Teleports the local player to a target transform when they interact with this object.
/// Demonstrates: Interact(), TeleportTo(), network event to teleport remote players' local instances.
///
/// IMPORTANT: Udon can only teleport the LOCAL player.
/// To teleport everyone, each client must call TeleportTo on themselves.
/// This script sends a network event so every client teleports their own local player.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class TeleportOnInteract : UdonSharpBehaviour
{
    [Header("Teleport Settings")]
    [Tooltip("Where to teleport the player.")]
    public Transform destination;

    [Tooltip("If true, teleports everyone. If false, only the player who interacted.")]
    public bool teleportEveryone = false;

    [Tooltip("If true, aligns the player's rotation with the destination. If false, rotation is unchanged.")]
    public bool alignRotation = true;

    public override void Interact()
    {
        if (teleportEveryone)
        {
            // Ask all clients to teleport their own local player
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(TeleportLocal));
        }
        else
        {
            TeleportLocal();
        }
    }

    /// <summary>
    /// Called on each client (either locally or via network event).
    /// Teleports the client's OWN local player to the destination.
    /// </summary>
    public void TeleportLocal()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local)) return;
        if (destination == null)
        {
            Debug.LogError("[TeleportOnInteract] Destination is not set!");
            return;
        }

        Quaternion rotation = alignRotation ? destination.rotation : local.GetRotation();

        local.TeleportTo(
            destination.position,
            rotation,
            VRC_SceneDescriptor.SpawnOrientation.Default,
            false // lerpOnRemote: false = instant for remote viewers
        );
    }
}
