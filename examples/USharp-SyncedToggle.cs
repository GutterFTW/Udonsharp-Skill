using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// Syncs a toggle state across all players using Manual sync + FieldChangeCallback.
/// Demonstrates:
///   - [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
///   - [UdonSynced] with [FieldChangeCallback]
///   - Ownership transfer before serialization
///   - Interact() to toggle state
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SyncedToggle : UdonSharpBehaviour
{
    [Header("References")]
    [Tooltip("The GameObject to show/hide based on the toggle state.")]
    public GameObject toggleObject;

    // The backing field — NEVER set this directly from outside this behaviour
    [UdonSynced, FieldChangeCallback(nameof(IsActive))]
    private bool _isActive = false;

    /// <summary>
    /// Property backed by _isActive.
    /// Setting it via the property always applies the visual change AND syncs to late joiners.
    /// FieldChangeCallback ensures remote clients call the setter when they receive the synced update.
    /// </summary>
    public bool IsActive
    {
        set
        {
            _isActive = value;
            ApplyState();
        }
        get => _isActive;
    }

    private void Start()
    {
        // Apply initial state locally
        ApplyState();
    }

    private void ApplyState()
    {
        if (toggleObject != null)
            toggleObject.SetActive(_isActive);
    }

    /// <summary>
    /// Called when a player interacts with this object.
    /// Takes ownership, flips the toggle, then sends the update to all clients.
    /// </summary>
    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        IsActive = !IsActive;
        RequestSerialization();
    }
}
