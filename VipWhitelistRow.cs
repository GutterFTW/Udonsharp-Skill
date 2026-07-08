using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class VipWhitelistRow : UdonSharpBehaviour
{
    [HideInInspector] public VipWhitelistUI parent;
    [HideInInspector] public TMP_Text nameText;
    [HideInInspector] public Toggle authToggle;
    [HideInInspector] public Toggle inWorldToggle; // cached in-world/present toggle
    [HideInInspector] public Toggle djToggle; // cached DJ toggle
    [HideInInspector] public int playerId = -1; // cached in-world player id, -1 if not associated

    // cached metadata (role membership/name) set at creation time since roles don't change at runtime
    [HideInInspector] public string cachedRawName;
    [HideInInspector] public string cachedRawNameLower;
    [HideInInspector] public string cachedDisplayName;
    [HideInInspector] public int cachedRoleIndex = -1;
    [HideInInspector] public bool cachedIsSuperAdmin = false;

    private bool lastIsOn;
    private bool lastDjIsOn;
    private bool started;

    void Start()
    {
        started = true;
        if (authToggle != null) lastIsOn = authToggle.isOn;
        if (djToggle != null) lastDjIsOn = djToggle.isOn;
    }

    // This method should be wired in the row prefab Toggle's OnValueChanged event
    // to call this behaviour (pass the bool). It forwards the event to the parent UI.
    public void _OnAuthToggle(bool isOn)
    {
        string playerName = "";
        if (nameText != null) playerName = nameText.text;
        if (parent != null)
        {
            parent._OnRowToggled(playerName, isOn);
        }
        lastIsOn = isOn;
    }

    // Called from DJ toggle in the row prefab via SendCustomEvent("DJToggled") on the row behaviour
    public void DJToggled()
    {
        // Prefer using cachedRawName (normalized/stripped) when available to avoid relying on displayed text
        string playerName = null;
        if (!string.IsNullOrEmpty(cachedRawName)) playerName = cachedRawName;
        if (string.IsNullOrEmpty(playerName)) playerName = nameText != null ? nameText.text : "";
        
        // Update lastDjIsOn to prevent PollToggleStates from detecting this as a change
        if (djToggle != null)
        {
            lastDjIsOn = djToggle.isOn;
        }
        
        if (parent != null)
        {
            parent.DJToggled(playerName);
        }
    }

    // Parameterless method to support UdonBehaviour.SendCustomEvent("AuthToggled") from the Toggle
    // inspector wiring. Reads the current toggle state and forwards it to the parent.
    public void AuthToggled()
    {
        bool isOn = false;
        if (authToggle != null) isOn = authToggle.isOn;
        string playerName = nameText != null ? nameText.text : "";

        // If Unity didn't flip the visual state (isOn == lastIsOn), assume the user intended to toggle and flip it ourselves.
        if (authToggle != null && isOn == lastIsOn)
        {
            bool desired = !lastIsOn;
            authToggle.SetIsOnWithoutNotify(desired);
            lastIsOn = desired;
            isOn = desired;
        }
        else
        {
            // Update lastIsOn to prevent PollToggleStates from detecting this as a change
            lastIsOn = isOn;
        }

        if (parent != null)
        {
            parent._OnRowToggled(playerName, isOn);
        }
    }

    // Public helpers to set toggle visual state without triggering Unity UI callbacks.
    // Use these from VipWhitelistUI whenever synchronizing toggles programmatically.
    public void SetAuthStateWithoutNotify(bool state)
    {
        if (authToggle != null)
        {
            authToggle.SetIsOnWithoutNotify(state);
        }
        lastIsOn = state;
        if (!started) started = true;
    }

    public void SetDjStateWithoutNotify(bool state)
    {
        if (djToggle != null)
        {
            djToggle.SetIsOnWithoutNotify(state);
        }
        lastDjIsOn = state;
        if (!started) started = true;
    }

}
