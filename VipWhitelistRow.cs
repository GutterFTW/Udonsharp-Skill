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

    void Start()
    {
        if (authToggle != null) lastIsOn = authToggle.isOn;
        if (djToggle != null) lastDjIsOn = djToggle.isOn;
    }

    // Called from DJ toggle in the row prefab via SendCustomEvent("DJToggled") on the row behaviour
    public void DJToggled()
    {
        // If Unity didn't flip the visual state (isOn == lastDjIsOn), assume the user intended to
        // toggle and flip it ourselves. Mirrors AuthToggled()'s self-correction below so a misread
        // first click doesn't require a second click to take effect. VipWhitelistUI.DJToggled()
        // re-reads djToggle.isOn after this, so correcting the toggle here is sufficient.
        if (djToggle != null)
        {
            bool isOn = djToggle.isOn;
            if (isOn == lastDjIsOn)
            {
                bool desired = !lastDjIsOn;
                djToggle.SetIsOnWithoutNotify(desired);
                lastDjIsOn = desired;
            }
            else
            {
                lastDjIsOn = isOn;
            }
        }

        // Prefer using cachedRawName (normalized/stripped) when available to avoid relying on displayed text
        string playerName = null;
        if (!string.IsNullOrEmpty(cachedRawName)) playerName = cachedRawName;
        if (string.IsNullOrEmpty(playerName)) playerName = nameText != null ? nameText.text : "";

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
            // Keep lastIsOn in sync with the toggle's actual state so the next AuthToggled() call
            // can correctly detect whether Unity flipped the visual state.
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
    }

    public void SetDjStateWithoutNotify(bool state)
    {
        if (djToggle != null)
        {
            djToggle.SetIsOnWithoutNotify(state);
        }
        lastDjIsOn = state;
    }

}
