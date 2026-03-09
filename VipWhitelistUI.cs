using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;
using VRC.Udon;
using System.Text;
using System;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class VipWhitelistUI : UdonSharpBehaviour
{
    // Auto-assigned by VipWhitelistManager.Start() — no inspector assignment needed.
    // The manager injects itself into all UIs listed in its 'lists[]' array before
    // VipWhitelistUI.Start() runs (guaranteed by [DefaultExecutionOrder(-100)] on the manager).
    [HideInInspector]
    public VipWhitelistManager manager;

    [Header("UI References (required)")]
    [Tooltip("Content transform (usually the Scroll View/Viewport/Content). Must be assigned in the inspector.")]
    public Transform contentRoot;
    [Tooltip("Row prefab/template containing a VipWhitelistRow. Must be assigned in the inspector.")]
    public GameObject rowTemplate;

    // Pool of recycled row GameObjects. Rows are reused from this array to avoid runtime allocations
    // and expensive Instantiate/Destroy calls when rows are added/removed.
    private GameObject[] rowPool = new GameObject[VipWhitelistManager.ROW_POOL_SIZE];
    private int rowPoolCount;
    // Normalized raw keys (no displayed role prefixes) for each active row. rowKeys[i] corresponds to rows[i].
    private string[] rowKeys = new string[VipWhitelistManager.MAX_ROWS];
    // Lowercased cached versions of rowKeys used for fast case-insensitive lookups. Kept in sync with rowKeys.
    private string[] rowKeysLower = new string[VipWhitelistManager.MAX_ROWS];

    // Buffer reused when calling VRCPlayerApi.GetPlayers to avoid allocating a new array each rebuild.
    private VRCPlayerApi[] playerBuf = new VRCPlayerApi[VipWhitelistManager.PLAYER_BUF_SIZE];
    // Active row GameObjects parented under the contentRoot. May contain pooled/inactive items when released.
    private GameObject[] rows = new GameObject[VipWhitelistManager.MAX_ROWS];
    // Cached VipWhitelistRow component for each active row GameObject to avoid frequent GetComponent calls.
    private VipWhitelistRow[] rowScripts = new VipWhitelistRow[VipWhitelistManager.MAX_ROWS];
    // Current number of active rows (valid indices are 0 .. rowCount-1)
    private int rowCount;

    private bool _started;
    private RectTransform _contentRect;

    [Header("DJ System")]
    [Tooltip("Assign the UI toggle that controls whether DJ whitelisting logic is enabled.")]
    public Toggle djSystemToggle;

    private bool _djSystemEnabled = true;
    private Toggle _templateDjToggle;
    private readonly string[] DJ_TOGGLE_HINTS = new string[] { "dj", "mix", "stage" };

    // Cached default name color from the row template TMP_Text. Used when no role or superadmin color applies.
    private Color _templateNameColor = Color.white;

    private VipWhitelistManager _cachedManager; // cached manager reference used for repeated queries (roles, colors, permissions)

    // Polling configuration: target updates per second for centralized polls of row toggles
    // Polling detects user interaction with per-row Toggles without a per-row Update() call.
    // Set to ~3Hz (0.33s interval) for better performance
    private float _pollIntervalSeconds = 0.33f;
    // Timestamp (realtime) when the last poll occurred
    private float _lastPollTime = -1f;

    // Player ID to displayName cache to reduce overhead of repeated player.displayName property access
    private int[] _playerIdToDisplayNameIds = new int[128];
    private string[] _playerIdToDisplayNames = new string[128];
    private int _playerIdToDisplayNameCount = 0;

    // Reusable buffer containing normalized (raw) names of players currently in world. Avoids per-frame allocations.
    private string[] _currentPlayerKeys = new string[VipWhitelistManager.PLAYER_BUF_SIZE];
    // Reusable lowercased key buffer used while building the seen-set in RebuildPlayerList to avoid allocations.
    private string[] _addedKeysLower = new string[256];

    // Cached LocalPlayer lookup per-frame to avoid repeated property access overhead in UI
    private VRCPlayerApi _cachedLocalPlayer = null;
    private int _cachedLocalPlayerFrame = -1;
    private VRCPlayerApi GetLocalPlayer()
    {
        int fc = Time.frameCount;
        if (_cachedLocalPlayerFrame != fc)
        {
            _cachedLocalPlayer = Networking.LocalPlayer;
            _cachedLocalPlayerFrame = fc;
        }
        return _cachedLocalPlayer;
    }

    // Cache whether the local player has an override to manage DJs (superadmin or initial owner) per-frame
    private bool _cachedLocalHasDjOverride = false;
    private int _cachedLocalHasDjOverrideFrame = -1;
    private bool GetLocalHasDjOverride()
    {
        int fc = Time.frameCount;
        if (_cachedLocalHasDjOverrideFrame != fc)
        {
            _cachedLocalHasDjOverrideFrame = fc;
            var lp = GetLocalPlayer();
            bool v = false;
            if (Utilities.IsValid(lp) && _cachedManager != null)
            {
                // Use cached displayName to avoid repeated property access
                string displayName = GetCachedPlayerDisplayName(lp);
                v = _cachedManager.IsSuperAdmin(displayName) || _cachedManager.IsInstanceInitialOwner(lp);
            }
            _cachedLocalHasDjOverride = v;
        }
        return _cachedLocalHasDjOverride;
    }

    // Cache whether the local player is a Super Admin per-frame
    private bool _cachedLocalIsSuperAdmin = false;
    private int _cachedLocalIsSuperAdminFrame = -1;
    private bool GetLocalIsSuperAdmin()
    {
        int fc = Time.frameCount;
        if (_cachedLocalIsSuperAdminFrame != fc)
        {
            _cachedLocalIsSuperAdminFrame = fc;
            var lp = GetLocalPlayer();
            bool v = false;
            if (Utilities.IsValid(lp) && _cachedManager != null)
            {
                string displayName = GetCachedPlayerDisplayName(lp);
                v = _cachedManager.IsSuperAdmin(displayName);
            }
            _cachedLocalIsSuperAdmin = v;
        }
        return _cachedLocalIsSuperAdmin;
    }

    // Cache manager.CanLocalManageDj() per-frame to avoid repeated calls
    private bool _cachedManagerCanManageDj = false;
    private int _cachedManagerCanManageDjFrame = -1;
    private bool GetCachedManagerCanManageDj()
    {
        int fc = Time.frameCount;
        if (_cachedManagerCanManageDjFrame != fc)
        {
            _cachedManagerCanManageDjFrame = fc;
            bool v = false;
            if (_cachedManager != null) v = _cachedManager.CanLocalManageDj();
            _cachedManagerCanManageDj = v;
        }
        return _cachedManagerCanManageDj;
    }

    // Get cached player displayName to avoid repeated property access overhead
    private string GetCachedPlayerDisplayName(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return null;
        int playerId = player.playerId;
        if (playerId < 0) return player.displayName; // invalid ID, just return directly
        
        // Check cache
        for (int i = 0; i < _playerIdToDisplayNameCount; i++)
        {
            if (_playerIdToDisplayNameIds[i] == playerId)
            {
                return _playerIdToDisplayNames[i];
            }
        }
        
        // Cache miss - store and return
        string displayName = player.displayName;
        if (_playerIdToDisplayNameCount < _playerIdToDisplayNameIds.Length)
        {
            int idx = _playerIdToDisplayNameCount++;
            _playerIdToDisplayNameIds[idx] = playerId;
            _playerIdToDisplayNames[idx] = displayName;
        }
        return displayName;
    }

    // Remove player from displayName cache when they leave
    private void RemovePlayerFromDisplayNameCache(int playerId)
    {
        if (playerId < 0) return;
        for (int i = 0; i < _playerIdToDisplayNameCount; i++)
        {
            if (_playerIdToDisplayNameIds[i] == playerId)
            {
                // Shift remaining entries down
                for (int j = i + 1; j < _playerIdToDisplayNameCount; j++)
                {
                    _playerIdToDisplayNameIds[j - 1] = _playerIdToDisplayNameIds[j];
                    _playerIdToDisplayNames[j - 1] = _playerIdToDisplayNames[j];
                }
                _playerIdToDisplayNameCount--;
                break;
            }
        }
    }

    

    void Start()
    {
        _started = true;

        // Require inspector-assigned references
        if (contentRoot == null)
        {
            if (manager != null && manager.enableDebugLogs)
            {
                string msg = "VipWhitelistUI: contentRoot must be assigned in the inspector.";
                manager.DebugLog(msg);
                Debug.LogError("(VIP Manager) " + msg);
            }

            this.enabled = false;
            return;
        }
        _contentRect = contentRoot.GetComponent<RectTransform>();
        if (_contentRect == null)
        {
            if (manager != null && manager.enableDebugLogs)
            {
                string msg = "VipWhitelistUI: contentRoot does not contain a RectTransform.";
                manager.DebugLog(msg);
                Debug.LogError("(VIP Manager) " + msg);
            }
            this.enabled = false;
            return;
        }

        if (rowTemplate == null)
        {
            if (manager != null && manager.enableDebugLogs)
            {
                string msg = "VipWhitelistUI: rowTemplate must be assigned in the inspector.";
                manager.DebugLog(msg);
                Debug.LogError("(VIP Manager) " + msg);
            }
            this.enabled = false;
            return;
        }
        // cache default name color from template (if present)
        if (Utilities.IsValid(rowTemplate))
        {
            var txt = rowTemplate.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (txt != null) _templateNameColor = txt.color;
            _templateDjToggle = FindToggleByName(rowTemplate, DJ_TOGGLE_HINTS);
            // ensure template is not active
            rowTemplate.SetActive(false);
        }

        if (manager == null)
        {
            // Manager not injected — this UI is not listed in the manager's 'lists[]' array.
            Debug.LogError("(VIP Manager) VipWhitelistUI: no manager assigned. Add this UI to the VipWhitelistManager's 'lists' array.");
            this.enabled = false;
            return;
        }

        _cachedManager = manager;
        // Register with the manager so this UI receives all future BroadcastDjSystemState() and
        // NotifyLists() calls. Without self-registration, only inspector-assigned entries in
        // lists[] receive updates — causing other UI panels in the scene to fall out of sync.
        manager.RegisterList(this);
        // Force the toggle visual to initialize correctly regardless of the default field value.
        // Pre-flip _djSystemEnabled so the guard in SetDjSystemEnabled never skips the initial set.
        bool initDjState = manager.GetSyncedDjSystemEnabled();
        _djSystemEnabled = !initDjState;
        SetDjSystemEnabled(initDjState);

        // Ensure manager role buffers are initialized so role colors/permissions are available
        _cachedManager.EnsureRoleBuffersInitialized();

        // initial full build
        RebuildPlayerList();
        // delegate access control visuals/objects to manager
        manager.EvaluateLocalAccess();
        manager.EvaluateLocalDjAccess();
    }

    void Update()
    {
        // Poll row toggles at target frequency (_pollIntervalSeconds) to capture user interactions
        // without per-row Update overhead. Use realtime to avoid being affected by time scale.
        float now = Time.realtimeSinceStartup;
        if (now - _lastPollTime >= _pollIntervalSeconds)
        {
            _lastPollTime = now;
            for (int i = 0; i < rowCount; i++)
            {
                var rs = rowScripts[i];
                if (rs == null) continue;
                rs.PollToggleStates();
            }
        }
    }

    private string NormalizeName(string s)
    {
        // Use exact trimmed names; do not lowercase or strip characters here.
        if (s == null) return null;
        return s.Trim();
    }

    // Strip any leading parenthesized prefixes repeatedly, e.g. "(DJ) (Role) Alice" -> "Alice"
    // Also strips trailing asterisk used for instance owner marker
    private string StripAllRolePrefixes(string displayed)
    {
        if (string.IsNullOrEmpty(displayed)) return displayed;
        string t = displayed.Trim();
        while (t.Length > 0 && t[0] == '(')
        {
            int idx = t.IndexOf(')');
            if (idx <= 0) break;
            if (idx + 1 >= t.Length) { t = ""; break; }
            t = t.Substring(idx + 1).Trim();
        }
        // Strip trailing asterisk (instance owner marker)
        if (t.EndsWith(" *"))
        {
            t = t.Substring(0, t.Length - 2).Trim();
        }
        return t;
    }

    private string NormalizeRawName(string displayed)
    {
        return NormalizeName(StripAllRolePrefixes(displayed));
    }

    // Strip a leading role prefix in parentheses from a displayed name, e.g. "(Role) Alice" -> "Alice"
    private string GetNameWithoutRolePrefix(string displayed)
    {
        if (string.IsNullOrEmpty(displayed)) return displayed;
        string t = displayed.Trim();
        if (t.Length == 0) return t;
        if (t[0] == '(')
        {
            int idx = t.IndexOf(')');
            if (idx > 0 && idx + 1 < t.Length)
            {
                string rest = t.Substring(idx + 1).Trim();
                return rest;
            }
            // if no closing paren found, fallthrough
        }
        return t;
    }

    // Detect if a displayed name contains an explicit "(Super Admin)" prefix
    private bool DisplayedIsSuperAdmin(string displayed)
    {
        if (string.IsNullOrEmpty(displayed)) return false;
        string t = displayed.Trim();
        if (t.Length > 0 && t[0] == '(')
        {
            int idx = t.IndexOf(')');
            if (idx > 0)
            {
                string prefix = t.Substring(1, idx - 1).Trim();
                if (!string.IsNullOrEmpty(prefix))
                {
                    string p = prefix.ToLowerInvariant();
                    if (p.Contains("super") && p.Contains("admin")) return true;
                }
            }
        }
        return false;
    }

    // Build display text with role prefix if applicable
    private string BuildDisplayName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        
        // Ensure we operate on the raw name without any existing displayed role prefixes
        string rawName = GetNameWithoutRolePrefix(raw);
        string role = null;
        bool isDj = false;
        bool isInstanceOwner = false;
        
        if (_cachedManager != null)
        {
            isDj = _cachedManager.IsDj(rawName);
            isInstanceOwner = _cachedManager.IsInstanceInitialOwner(rawName);
            
            if (_cachedManager.IsSuperAdmin(rawName))
            {
                role = "Super Admin";
            }
            else
            {
                int ridx = _cachedManager.GetRoleIndex(rawName);
                if (ridx >= 0)
                {
                    string rn = _cachedManager.GetRoleNameByIndex(ridx);
                    if (!string.IsNullOrEmpty(rn)) role = rn;
                }
                else
                {
                    // Check manual list via cached access result (avoids O(manualCount) scan per row per rebuild)
                    if (_cachedManager.IsAuthed(rawName))
                    {
                        role = "VIP";
                    }
                }
            }
        }

        string display = rawName.Trim();
        
        // Add asterisk for instance owner
        if (isInstanceOwner)
        {
            display = display + " *";
        }
        
        // Add role prefix if applicable (skip if role is "DJ" since we handle that separately)
        if (!string.IsNullOrEmpty(role))
        {
            string roleLower = role.Trim().ToLowerInvariant();
            if (roleLower != "dj")
            {
                display = "(" + role + ") " + display;
            }
        }

        // Add DJ prefix if applicable — manual character check avoids ToLowerInvariant string allocation
        if (isDj)
        {
            bool alreadyHasDjPrefix = display.Length >= 4 &&
                display[0] == '(' &&
                (display[1] == 'd' || display[1] == 'D') &&
                (display[2] == 'j' || display[2] == 'J') &&
                display[3] == ')';
            if (!alreadyHasDjPrefix)
            {
                display = "(DJ) " + display;
            }
        }

        return display;
    }

    

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        // incremental: add only the joining player row
        if (!Utilities.IsValid(player)) return;
        string displayName = GetCachedPlayerDisplayName(player);
        bool authed = IsAuthed(displayName);
        AddRowIfMissing(displayName, true, authed, player.playerId);
        // If the row already existed (authed player rejoining), AddRowIfMissing returned early
        // without updating the playerId. Update it now so FindRowIndexByPlayerId works on next leave.
        string rejoinKey = NormalizeRawName(displayName);
        int rejoinIdx = FindRowIndexByLower(rejoinKey);
        if (rejoinIdx != -1)
        {
            var rejoinRs = rowScripts[rejoinIdx];
            if (rejoinRs != null) rejoinRs.playerId = player.playerId;
        }
        UpdateRowForName(displayName, authed);
        // no full rebuild
        if (manager != null) manager.EvaluateLocalAccess();
        // Only evaluate DJ access if the joining player is the local player (others won't affect local DJ state)
        if (manager != null)
        {
            var lp = Networking.LocalPlayer;
            if (Utilities.IsValid(lp) && lp.playerId == player.playerId) manager.EvaluateLocalDjAccess(true);
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        // incremental: remove only the leaving player row
        if (!Utilities.IsValid(player))
        {
            // Can't determine who left; avoid full rebuild to reduce cost. Just update access.
            if (manager != null) manager.EvaluateLocalAccess();
            return;
        }

        string dn = GetCachedPlayerDisplayName(player);
        int pid = player.playerId;
        
        // Remove from display name cache
        RemovePlayerFromDisplayNameCache(pid);

        // Try to find matching row by playerId first (more reliable than displayName)
        int idxById = -1;
        if (pid >= 0) idxById = FindRowIndexByPlayerId(pid);

        if (idxById != -1)
        {
            var rs = rowScripts[idxById];
            string rawKey = rowKeys[idxById];
            bool isAuthed = IsAuthed(rawKey);
            if (isAuthed)
            {
                // mark as not in-world and clear cached playerId so row remains for rejoin
                if (rs != null && rs.inWorldToggle != null)
                {
                    rs.inWorldToggle.SetIsOnWithoutNotify(false);
                }
                else
                {
                    var rowObj = rows[idxById];
                    if (Utilities.IsValid(rowObj))
                    {
                        var toggles = rowObj.GetComponentsInChildren<Toggle>(true);
                        for (int t = 0; t < toggles.Length; t++)
                        {
                            string n = toggles[t].gameObject.name.ToLowerInvariant();
                            if (n.Contains("here") || n.Contains("inworld") || n.Contains("present"))
                            {
                                toggles[t].SetIsOnWithoutNotify(false);
                                break;
                            }
                        }
                    }
                }
                if (rs != null) rs.playerId = -1;
            }
            else
            {
                // not authed -> remove row
                // Add bounds checking for idxById before accessing arrays
                string nameToRemove = null;
                
                // Always check bounds first to prevent index out of range
                if (idxById >= 0 && idxById < rowCount && idxById < rowKeys.Length)
                {
                    if (rs != null && rs.nameText != null)
                    {
                        nameToRemove = rs.nameText.text;
                    }
                    else
                    {
                        nameToRemove = rowKeys[idxById];
                    }
                }
                
                if (!string.IsNullOrEmpty(nameToRemove))
                {
                    RemoveRow(nameToRemove);
                }
            }
        }
        else
        {
            // Fallback: match by displayName if playerId lookup fails
            if (string.IsNullOrEmpty(dn))
            {
                if (manager != null) manager.EvaluateLocalAccess();
                return;
            }

            bool isAuthed = IsAuthed(dn);
            if (isAuthed)
            {
                string normalized = NormalizeRawName(dn);
                int idx = FindRowIndexByLower(normalized);
                if (idx != -1)
                {
                    var rs2 = rowScripts[idx];
                    if (rs2 != null && rs2.inWorldToggle != null)
                    {
                        rs2.inWorldToggle.SetIsOnWithoutNotify(false);
                    }
                    else
                    {
                        // fallback: set via generic helper
                        var rowObj = rows[idx];
                        if (Utilities.IsValid(rowObj))
                        {
                            var toggles = rowObj.GetComponentsInChildren<Toggle>(true);
                            for (int t = 0; t < toggles.Length; t++)
                            {
                                string n = toggles[t].gameObject.name.ToLowerInvariant();
                                if (n.Contains("here") || n.Contains("inworld") || n.Contains("present"))
                                {
                                    toggles[t].SetIsOnWithoutNotify(false);
                                    break;
                                }
                            }
                        }
                    }
                    // Clear stale playerId so a future player with the same ID doesn't match
                    // this row. The primary (idxById != -1) path clears playerId; this fallback
                    // path previously forgot to, leaving a dangling ID that VRChat can reuse.
                    if (rs2 != null) rs2.playerId = -1;
                }
            }
            else
            {
                RemoveRow(dn);
            }
        }

        if (manager != null) manager.EvaluateLocalAccess();
        if (manager != null) manager.EvaluateLocalDjAccess();
    }

    private void RemoveRow(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return;
        string normalized = NormalizeRawName(displayName);
        if (string.IsNullOrEmpty(normalized)) return;
        int idx = FindRowIndexByLower(normalized);
        if (idx < 0 || idx >= rowCount) return;

        var go = rows[idx];
        if (Utilities.IsValid(go)) ReleaseRowToPool(go);

        for (int i = idx + 1; i < rowCount; i++)
        {
            rows[i - 1] = rows[i];
            rowScripts[i - 1] = rowScripts[i];
            rowKeys[i - 1] = rowKeys[i];
            if (rowKeysLower != null && i < rowKeysLower.Length) rowKeysLower[i - 1] = rowKeysLower[i];
        }

        rowCount--;
        if (rowCount < 0) rowCount = 0;
        if (rowCount < rows.Length) rows[rowCount] = null;
        if (rowCount < rowScripts.Length) rowScripts[rowCount] = null;
        if (rowCount < rowKeys.Length) rowKeys[rowCount] = null;
        if (rowKeysLower != null && rowCount < rowKeysLower.Length) rowKeysLower[rowCount] = null;
    }

    public void UpdateRowForName(string playerName, bool isAuthed)
    {
        if (string.IsNullOrEmpty(playerName)) return;
        string normalized = NormalizeRawName(playerName);
        if (string.IsNullOrEmpty(normalized)) return;
        int idx = FindRowIndexByLower(normalized);
        if (idx < 0 || idx >= rowCount) return;

        var rowObj = rows[idx];
        if (!Utilities.IsValid(rowObj)) return;

        // Use cached rowScripts[idx] to avoid a GetComponent call inside SetRowAuth.
        var rsIdx = rowScripts[idx];
        if (rsIdx != null) rsIdx.SetAuthStateWithoutNotify(isAuthed);
        else SetRowAuth(rowObj, isAuthed);
        string key = rowKeys[idx];
        bool canEdit = CanLocalEditTarget(key);
        SetRowInteractable(rowObj, canEdit, idx);
        SetRowDjInteractable(rowObj, GetCachedManagerCanManageDj(), idx);

        rowKeys[idx] = normalized;
        if (rowKeysLower == null || rowKeysLower.Length <= idx) rowKeysLower = new string[Mathf.Max(rowKeys.Length, idx + 16)];
        rowKeysLower[idx] = normalized.ToLowerInvariant();

        var rs = rowScripts[idx];
        if (rs != null)
        {
            rs.cachedRawName = normalized;
            string display = BuildDisplayName(normalized);
            if (rs.nameText != null && rs.nameText.text != display)
            {
                rs.nameText.text = display;
            }
            rs.cachedDisplayName = display;
            if (_cachedManager != null)
            {
                rs.cachedIsSuperAdmin = _cachedManager.IsSuperAdmin(normalized);
                rs.cachedRoleIndex = !string.IsNullOrEmpty(normalized) ? _cachedManager.GetRoleIndex(normalized) : -1;
            }
        }
    }

    private int FindRowIndexByPlayerId(int pid)
    {
        if (pid < 0) return -1;
        for (int i = 0; i < rowCount; i++)
        {
            var rs = rowScripts[i];
            if (rs == null) continue;
            if (rs.playerId == pid) return i;
        }
        return -1;
    }

    // Build the rows to reflect current players only
    public void RebuildPlayerList()
    {
        if (manager != null && manager.enableDebugLogs)
        {
            manager.DebugLog("RebuildPlayerList: Starting player list rebuild");
        }
        
        // Preserve scroll/content position so UI doesn't jump to top on rebuild
        // Incremental approach: do not destroy existing rows. Add any missing players to the bottom,
        // and ensure manual entries are shown. Deletion of rows is handled by OnPlayerLeft which
        // removes the specific row.
        int totalPlayers = VRCPlayerApi.GetPlayerCount();
        
        if (manager != null && manager.enableDebugLogs)
        {
            manager.DebugLog("RebuildPlayerList: Player count = " + totalPlayers.ToString());
        }
        
        if (totalPlayers > playerBuf.Length) playerBuf = new VRCPlayerApi[totalPlayers];
        int count = totalPlayers;
        VRCPlayerApi.GetPlayers(playerBuf);

        // keep local seen set (lowercased) to avoid adding duplicates when players belong to multiple roles
        if (_addedKeysLower == null || _addedKeysLower.Length < rows.Length) _addedKeysLower = new string[Mathf.Max(rows.Length, 256)];
        int addedKeysCount = 0;
        
        // Pre-populate _addedKeysLower with existing rows to prevent duplicates
        for (int i = 0; i < rowCount; i++)
        {
            if (i < rowKeysLower.Length && !string.IsNullOrEmpty(rowKeysLower[i]))
            {
                if (addedKeysCount < _addedKeysLower.Length)
                {
                    _addedKeysLower[addedKeysCount++] = rowKeysLower[i];
                }
            }
        }

        // Build a quick lookup of current in-world player normalized names
        // (strip any role prefix and normalize)
        // Reuse cached buffer to avoid allocations
        if (_currentPlayerKeys == null || _currentPlayerKeys.Length < count) _currentPlayerKeys = new string[count];
        int currentPlayersCount = 0;
        for (int i = 0; i < count; i++)
        {
            var p = playerBuf[i];
            if (!Utilities.IsValid(p)) continue;
            string displayName = GetCachedPlayerDisplayName(p);
            string key = NormalizeRawName(displayName);
            if (string.IsNullOrEmpty(key)) continue; // Skip invalid names early
            
            string keyLower = key.ToLowerInvariant();
            
            // Build current players list for in-world toggle updates
            // Always add in-world players to current keys BEFORE checking for duplicates
            if (currentPlayersCount < _currentPlayerKeys.Length)
            {
                _currentPlayerKeys[currentPlayersCount++] = key;
                
                if (manager != null && manager.enableDebugLogs)
                {
                    manager.DebugLog("RebuildPlayerList: Added '" + key + "' to currentPlayerKeys, count now = " + currentPlayersCount.ToString());
                }
            }
            
            // Skip if already added (case-insensitive). Linear search is optimal for UdonSharp (no HashSet available)
            bool already = false;
            for (int _a = 0; _a < addedKeysCount; _a++)
            {
                if (_addedKeysLower[_a] == keyLower)
                {
                    already = true;
                    break;
                }
            }
            if (already) continue;
            
            // ensure row exists for this player (will add to bottom if missing)
            bool authed = IsAuthed(displayName);
            int existingIdx = FindRowIndexByLower(key);
            
            if (manager != null && manager.enableDebugLogs)
            {
                manager.DebugLog("RebuildPlayerList: Processing player '" + displayName + "' (key: '" + key + "'), authed=" + authed.ToString() + ", existingIdx=" + existingIdx.ToString());
            }
            
            if (existingIdx == -1)
            {
                AddRowIfMissing(displayName, true, authed, p.playerId);
            }
            else
            {
                // update cached playerId for the existing row in case it was previously a manual entry
                var rs = rowScripts[existingIdx];
                if (rs != null) rs.playerId = p.playerId;
            }
            
            // Track this key as processed to prevent duplicates (moved outside the if block)
            if (addedKeysCount < _addedKeysLower.Length)
            {
                _addedKeysLower[addedKeysCount++] = keyLower;
            }
        }

        // Ensure manually granted VIPs are shown even if they're not currently in world
        if (manager != null)
        {
            int mc = manager.GetManualCount();
            for (int i = 0; i < mc; i++)
            {
                string manual = manager.GetManualAt(i);
                if (string.IsNullOrEmpty(manual)) continue;
                string key = NormalizeRawName(manual);
                if (string.IsNullOrEmpty(key)) continue; // Skip invalid names early
                
                string keyLower = key.ToLowerInvariant();
                // Skip duplicates (linear search is optimal for UdonSharp - no HashSet available)
                bool already = false;
                for (int _a = 0; _a < addedKeysCount; _a++)
                {
                    if (_addedKeysLower[_a] == keyLower)
                    {
                        already = true;
                        break;
                    }
                }
                if (already) continue;
                if (FindRowIndexByLower(key) == -1)
                {
                    AddRowIfMissing(manual, false, true, -1);
                }
                
                // Track this key as processed to prevent duplicates (moved outside the if block)
                if (addedKeysCount < _addedKeysLower.Length)
                {
                    _addedKeysLower[addedKeysCount++] = keyLower;
                }
            }
        }

        // Update in-world toggles and auth states for existing rows without rebuilding the entire list
        for (int i = 0; i < rowCount; i++)
        {
            var r = rows[i];
            var rs = rowScripts[i];
            if (rs == null || r == null) continue;

            // Prefer cached raw name (set at creation) to avoid parsing displayed text repeatedly
            string raw = rs.cachedRawName;
            if (string.IsNullOrEmpty(raw))
            {
                var txt = rs.nameText;
                string displayed = txt != null ? txt.text : null;
                raw = GetNameWithoutRolePrefix(displayed);
                raw = NormalizeName(raw);
                rs.cachedRawName = raw;
            }

            // set in-world toggle if present using cached toggle when possible
            Toggle inWorldToggle = rs.inWorldToggle;
            Toggle authToggle = rs.authToggle;

            if (inWorldToggle != null)
            {
                bool present = false;
                if (!string.IsNullOrEmpty(raw))
                {
                    for (int _k = 0; _k < currentPlayersCount; _k++)
                    {
                        if (_currentPlayerKeys[_k] == raw)
                        {
                            present = true;
                            break;
                        }
                    }
                }
                
                if (manager != null && manager.enableDebugLogs && inWorldToggle.isOn != present)
                {
                    manager.DebugLog("Updating 'Here' toggle for '" + raw + "': was=" + inWorldToggle.isOn.ToString() + ", setting to=" + present.ToString() + " (currentPlayersCount=" + currentPlayersCount.ToString() + ")");
                }
                
                if (inWorldToggle.isOn != present) inWorldToggle.SetIsOnWithoutNotify(present);
            }

            // Recompute cached role metadata so colors/permissions reflect latest role lists
            if (_cachedManager != null)
            {
                rs.cachedIsSuperAdmin = _cachedManager.IsSuperAdmin(raw);
                rs.cachedRoleIndex = _cachedManager.GetRoleIndex(raw);
                // also respect explicit displayed "(Super Admin)" prefix if present in cached display name
                if (!rs.cachedIsSuperAdmin && rs.cachedDisplayName != null && DisplayedIsSuperAdmin(rs.cachedDisplayName)) rs.cachedIsSuperAdmin = true;
            }

            // update auth toggle and interactable/color via helper methods
            bool authed = IsAuthed(raw);
            rs.SetAuthStateWithoutNotify(authed); // rs is non-null here, skip GetComponent
            // ensure interactable reflects permissions using centralized helper
            bool canEdit = CanLocalEditTarget(raw);
            SetRowInteractable(r, canEdit, i);

            // update name color using cached metadata (and ensure display text uses role prefix)
            var txtComp = rs.nameText;
            if (txtComp != null && _cachedManager != null)
            {
                string display = BuildDisplayName(raw);
                if (txtComp.text != display) txtComp.text = display;
                Color desiredColor = _templateNameColor;
                if (rs.cachedIsSuperAdmin) desiredColor = _cachedManager.superAdminNameColor;
                else if (rs.cachedRoleIndex >= 0) desiredColor = _cachedManager.GetRoleColorByIndex(rs.cachedRoleIndex);
                if (txtComp.color != desiredColor) txtComp.color = desiredColor;
                // also ensure DJ toggle reflects state
                Toggle dj = ResolveDjToggle(rs, r);
                if (dj != null)
                {
                    bool isDj = _cachedManager != null ? _cachedManager.IsDj(raw) : false;
                    // keep polling state consistent: update rowScript when available to avoid PollToggleStates treating this as a user event
                    if (rs != null) rs.SetDjStateWithoutNotify(isDj);
                    else if (dj.isOn != isDj) dj.SetIsOnWithoutNotify(isDj);
                }
                SetRowDjInteractable(r, GetCachedManagerCanManageDj(), i);
            }
        }

        if (manager != null && manager.enableDebugLogs)
        {
            manager.DebugLog("RebuildPlayerList: Completed. Total rows: " + rowCount.ToString());
        }
    }

    // Optimized row index lookup using cached lowercased keys
    // Expects a normalized player name (already stripped of role prefixes via NormalizeRawName)
    private int FindRowIndexByLower(string normalizedName)
    {
        if (string.IsNullOrEmpty(normalizedName)) return -1;
        
        string keyLower = normalizedName.ToLowerInvariant();
        
        // Fast-path: use cached lowercased rowKeys when available
        if (rowKeysLower != null)
        {
            int rowKeysLowerLen = rowKeysLower.Length;
            int maxIndex = Mathf.Min(rowCount, rowKeysLowerLen);
            
            for (int i = 0; i < maxIndex; i++)
            {
                string rk = rowKeysLower[i];
                if (string.IsNullOrEmpty(rk)) continue;
                if (rk == keyLower) return i;
            }
        }
        
        // Fallback: scan displayed text (slower). This path should rarely execute if rowKeysLower is properly maintained.
        // Note: ToLowerInvariant on cachedRawName is suboptimal. Consider caching a lowercased version in VipWhitelistRow
        // if this fallback path becomes performance-critical (would require adding cachedRawNameLower field).
        for (int i = 0; i < rowCount; i++)
        {
            var rs = rowScripts[i];
            if (rs == null) continue;
            
            // Prefer cached raw name to avoid parsing displayed text
            string raw = rs.cachedRawName;
            if (!string.IsNullOrEmpty(raw))
            {
                if (raw.ToLowerInvariant() == keyLower)
                {
                    return i;
                }
            }
            else
            {
                // Fall back to parsing displayed text with null check
                var txt = rs.nameText;
                if (txt == null) continue;
                
                string rn = txt.text;
                if (!string.IsNullOrEmpty(rn))
                {
                    string cand = NormalizeRawName(rn);
                    if (!string.IsNullOrEmpty(cand) && cand.ToLowerInvariant() == keyLower)
                    {
                        return i;
                    }
                }
            }
        }
        
        return -1;
    }

    // Only role system + superadmin supported; manager implements default logic if needed
    private bool CanLocalEditAny()
    {
        if (manager != null) return manager.CanLocalEditAny();
        return false;
    }

    private bool CanLocalEditTarget(string targetName)
    {
        if (manager != null) return manager.CanLocalEditTarget(targetName);
        return false;
    }

    private void AddRowIfMissing(string displayName, bool inWorld, bool authed, int inWorldPlayerId = -1)
    {
        if (string.IsNullOrEmpty(displayName)) return;
        // Compare rows using names without any displayed role prefix so duplicates don't get created
        string lower = NormalizeRawName(displayName);
        if (FindRowIndexByLower(lower) != -1) return;
        if (rowCount >= rows.Length) return; // capacity reached

        if (!Utilities.IsValid(rowTemplate) || !Utilities.IsValid(contentRoot)) return;
        GameObject go = GetPooledRow();
        if (go == null) return;
        if (!Utilities.IsValid(go)) return;
        go.transform.SetParent(contentRoot, false);
        go.SetActive(true);
        
        if (manager != null && manager.enableDebugLogs)
        {
            manager.DebugLog("AddRowIfMissing: Creating row for '" + displayName + "', inWorld=" + inWorld.ToString() + ", authed=" + authed.ToString());
        }

        // find UI components inside the instantiated prefab
        TMPro.TMP_Text nameText = go.GetComponentInChildren<TMPro.TMP_Text>(true);
        Toggle authToggle = null;
        Toggle inWorldToggle = null;
        Toggle foundDjToggle = null;
        // find toggles by name hints first
        inWorldToggle = FindToggleByName(go, new string[] { "here", "inworld", "present" });
        authToggle = FindToggleByName(go, new string[] { "auth", "allow" });
        foundDjToggle = FindToggleByName(go, DJ_TOGGLE_HINTS);
        // get all toggles in the row
        Toggle[] toggles = go.GetComponentsInChildren<Toggle>(true);
        // fallback: assign first/second toggles
        if (authToggle == null && inWorldToggle != null) authToggle = inWorldToggle;
        if (inWorldToggle == null && toggles.Length > 1) inWorldToggle = toggles[1];
        // set up values
        // For in-world players show role prefixes; for manual/temporary entries avoid adding a (VIP) prefix
        if (nameText != null)
        {
            if (inWorld) nameText.text = BuildDisplayName(displayName);
            else nameText.text = GetNameWithoutRolePrefix(displayName).Trim();
        }


        // cache normalized raw key to avoid repeated string operations
        string cachedKey = NormalizeRawName(nameText != null ? nameText.text : displayName);
        rowKeys[rowCount] = cachedKey;
        if (rowKeysLower == null || rowKeysLower.Length <= rowCount) rowKeysLower = new string[Mathf.Max(rowKeys.Length, rowCount + 16)];
        rowKeysLower[rowCount] = string.IsNullOrEmpty(cachedKey) ? null : cachedKey.ToLowerInvariant();


        // determine colors once
        Color nameColor = _templateNameColor;
            if (manager != null)
            {
                // raw is the cached normalized name without display prefixes
                string raw = rowKeys[rowCount];
            // Super Admin takes priority over role colors
            // Prefer explicit displayed prefix first (handles manual entries like "(Super Admin) Name")
            if (nameText != null && DisplayedIsSuperAdmin(nameText.text))
            {
                nameColor = _cachedManager.superAdminNameColor;
            }
            else if (!string.IsNullOrEmpty(raw) && _cachedManager.IsSuperAdmin(raw))
            {
                nameColor = _cachedManager.superAdminNameColor;
            }
                else
                {
                    int ridx = !string.IsNullOrEmpty(raw) ? _cachedManager.GetRoleIndex(raw) : -1;
                    if (ridx >= 0)
                    {
                        nameColor = _cachedManager.GetRoleColorByIndex(ridx);
                    }
                    else
                    {
                        // fall back to template color
                        nameColor = _templateNameColor;
                    }
                }
        }
        else
        {
            nameColor = _templateNameColor;
        }

        if (nameText != null)
        {
            nameText.color = nameColor;
        }
        if (authToggle != null)
        {
            // set without notifying listeners to avoid firing events
            // defer setting the internal poll state until rowScript is wired so PollToggleStates won't treat this as user input
            authToggle.SetIsOnWithoutNotify(authed);
            string rawName = rowKeys[rowCount];
            bool canEditTarget = CanLocalEditTarget(rawName);
            // Super Admin rows are permanent and uneditable by anyone
            if (manager != null && !string.IsNullOrEmpty(rawName) && manager.IsSuperAdmin(rawName)) authToggle.interactable = false;
            else authToggle.interactable = canEditTarget;
        }
        if (foundDjToggle != null)
        {
            bool isDj = _cachedManager != null ? _cachedManager.IsDj(rowKeys[rowCount]) : false;
            // set without notifying; we'll sync poll-state on the rowScript once wired
            foundDjToggle.SetIsOnWithoutNotify(isDj);
            bool canToggleDj = _cachedManager != null ? _cachedManager.CanLocalManageDj() : false;
            // use centralized helper to set DJ interactivity so rules (superadmin/read-only) are enforced consistently
            SetRowDjInteractable(go, canToggleDj, rowCount);
        }
        if (inWorldToggle != null)
        {
            inWorldToggle.SetIsOnWithoutNotify(inWorld);
            inWorldToggle.interactable = false; // display-only
        }

        // Instead of adding listeners at runtime (not supported by Udon), forward toggle events using a row helper behaviour
        var rowScript = go.GetComponent<VipWhitelistRow>();
        if (rowScript != null)
        {
            rowScript.parent = this;
            rowScript.nameText = nameText;
            rowScript.authToggle = authToggle;
            rowScript.inWorldToggle = inWorldToggle;
            // if row prefab contains a DJ toggle, wire it via rowScript
            if (foundDjToggle == null) foundDjToggle = go.GetComponentInChildren<Toggle>(true);
            // The prefab should have a named DJ toggle; FindToggleByName can be used by designers
            // but leave wiring to prefab if a method is set to call rowScript.DJToggled
            // cache associated player id if this row represents an in-world player
            rowScript.playerId = inWorld ? inWorldPlayerId : -1;
            // cache raw/display names and role metadata since roles do not change at runtime
            // Use NormalizeRawName (strips ALL prefixes) so cachedRawName never retains leftover
            // role/DJ prefixes — previously used GetNameWithoutRolePrefix which only stripped one level.
            string rawName = NormalizeRawName(nameText != null ? nameText.text : displayName);
            rowScript.cachedRawName = rawName;
            rowScript.cachedDisplayName = nameText != null ? nameText.text : displayName;
            if (_cachedManager != null)
            {
                rowScript.cachedIsSuperAdmin = !string.IsNullOrEmpty(rawName) && _cachedManager.IsSuperAdmin(rawName);
                rowScript.cachedRoleIndex = !string.IsNullOrEmpty(rawName) ? _cachedManager.GetRoleIndex(rawName) : -1;
            }

            // Also respect explicit displayed "(Super Admin)" prefix in cached flags so UI color/permissions reflect it
            if (!rowScript.cachedIsSuperAdmin && rowScript.cachedDisplayName != null && DisplayedIsSuperAdmin(rowScript.cachedDisplayName))
            {
                rowScript.cachedIsSuperAdmin = true;
            }
            // assign found DJ toggle to row script for polling
            rowScript.djToggle = foundDjToggle;

            // Now that the rowScript is wired, update its internal poll-tracking state to match the visual toggles we set earlier.
            // This prevents PollToggleStates from misinterpreting programmatic updates as user toggles.
            if (rowScript.authToggle != null) rowScript.SetAuthStateWithoutNotify(rowScript.authToggle.isOn);
            if (rowScript.djToggle != null) rowScript.SetDjStateWithoutNotify(rowScript.djToggle.isOn);
         }

        rows[rowCount] = go;
        rowScripts[rowCount] = rowScript;
        rowCount++;
    }

    private GameObject GetPooledRow()
    {
        // Reuse a pooled row if available to avoid runtime allocations
        while (rowPoolCount > 0)
        {
            rowPoolCount--;
            GameObject pooled = rowPool[rowPoolCount];
            rowPool[rowPoolCount] = null;
            if (Utilities.IsValid(pooled)) return pooled;
        }
        if (Utilities.IsValid(rowTemplate))
        {
            return Instantiate(rowTemplate);
        }
        return null;
    }

    private void ReleaseRow(GameObject go)
    {
        if (!Utilities.IsValid(go)) return;
        // Return to pool for reuse instead of destroying
        if (rowPoolCount < rowPool.Length)
        {
            go.SetActive(false);
            go.transform.SetParent(null, false);
            rowPool[rowPoolCount++] = go;
        }
        else
        {
            GameObject.Destroy(go);
        }
    }

    private Toggle FindToggleByName(GameObject root, string[] hints)
    {
        if (!Utilities.IsValid(root) || hints == null || hints.Length == 0) return null;
        var toggles = root.GetComponentsInChildren<Toggle>(true);
        if (toggles == null) return null;
        for (int i = 0; i < toggles.Length; i++)
        {
            var toggle = toggles[i];
            if (!Utilities.IsValid(toggle)) continue;
            string name = toggle.gameObject.name.ToLowerInvariant();
            for (int j = 0; j < hints.Length; j++)
            {
                string hint = hints[j];
                if (string.IsNullOrEmpty(hint)) continue;
                if (name.Contains(hint)) return toggle;
            }
        }
        return null;
    }


    // Helper: Get auth toggle from row (uses cached reference when available)
    private Toggle GetAuthToggle(GameObject row, VipWhitelistRow rowScript)
    {
        if (!Utilities.IsValid(row)) return null;
        
        // Use cached toggle if available
        if (rowScript != null && rowScript.authToggle != null)
        {
            return rowScript.authToggle;
        }
        
        // Fallback: search by name
        var toggles = row.GetComponentsInChildren<Toggle>(true);
        if (toggles == null || toggles.Length == 0) return null;
        
        int togglesLen = toggles.Length;
        for (int i = 0; i < togglesLen; i++)
        {
            string n = toggles[i].gameObject.name.ToLowerInvariant();
            if (n.Contains("auth") || n.Contains("allow"))
            {
                return toggles[i];
            }
        }
        
        return toggles.Length > 0 ? toggles[0] : null;
    }

    // Use cached toggles from VipWhitelistRow when available to avoid repeated GetComponentsInChildren calls.
    // Pass a valid idx to use the pre-cached rowScripts[] entry and skip the GetComponent call entirely.
    private void SetRowAuth(GameObject row, bool authed, int idx = -1)
    {
        if (!Utilities.IsValid(row)) return;
        
        var rowScript = (idx >= 0 && idx < rowCount) ? rowScripts[idx] : null;
        if (rowScript == null) rowScript = row.GetComponent<VipWhitelistRow>();
        
        // Prefer rowScript helper to keep poll-tracking in sync
        if (rowScript != null)
        {
            rowScript.SetAuthStateWithoutNotify(authed);
            return;
        }

        var auth = GetAuthToggle(row, null);
        if (auth != null)
        {
            auth.SetIsOnWithoutNotify(authed);
        }
    }

    private void SetRowInteractable(GameObject row, bool canEdit, int idx = -1)
    {
        if (!Utilities.IsValid(row)) return;
        
        VipWhitelistRow rs = null;
        if (idx >= 0 && idx < rowCount)
        {
            rs = rowScripts[idx];
        }
        if (rs == null)
        {
            rs = row.GetComponent<VipWhitelistRow>();
        }
        
        Toggle auth = GetAuthToggle(row, rs);
        if (auth == null) return;

        string raw = null;
        if (idx >= 0 && idx < rowCount)
        {
            raw = rowKeys[idx];
        }
        else
        {
            var txt = row.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (txt != null) raw = GetNameWithoutRolePrefix(txt.text);
        }

        // Prefer cached role index from the row script when available to avoid extra string parsing.
        int roleIdx = -1;
        if (_cachedManager != null)
        {
            if (idx >= 0 && idx < rowCount)
            {
                var rsc2 = rowScripts[idx];
                if (rsc2 != null) roleIdx = rsc2.cachedRoleIndex;
            }
            if (roleIdx < 0 && !string.IsNullOrEmpty(raw))
            {
                roleIdx = _cachedManager.GetRoleIndex(raw);
            }
            
            // If the target belongs to a role marked Read-Only, make the auth toggle non-interactable for everyone.
            if (roleIdx >= 0 && _cachedManager.GetRoleIsReadOnly(roleIdx))
            {
                auth.interactable = false;
                return;
            }
        }

        // Use cached superadmin flag if available (superadmins are not editable by default)
        if (idx >= 0 && idx < rowCount)
        {
            var rsc = rowScripts[idx];
            if (rsc != null && rsc.cachedIsSuperAdmin)
            {
                auth.interactable = false;
                return;
            }
        }
        else if (!string.IsNullOrEmpty(raw) && _cachedManager != null && _cachedManager.IsSuperAdmin(raw))
        {
            auth.interactable = false;
            return;
        }

        auth.interactable = canEdit;
    }

    // Set DJ toggle interactivity using the same prefer-cached approach as SetRowInteractable.
    // This centralizes all interactivity control for DJ toggles so callers should use this method
    // instead of assigning .interactable directly.
    private void SetRowDjInteractable(GameObject row, bool canManageDj, int idx = -1)
    {
        if (!Utilities.IsValid(row)) return;
        VipWhitelistRow rs = null;
        if (idx >= 0 && idx < rowCount) rs = rowScripts[idx];
        Toggle dj = ResolveDjToggle(rs, row);
        UpdateDjToggleVisibility(dj, _djSystemEnabled);
        if (!_djSystemEnabled || !Utilities.IsValid(dj)) return;

        string raw = null;
        if (idx >= 0 && idx < rowCount)
        {
            raw = rowKeys[idx];
        }
        else
        {
            var txt = row.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (txt != null) raw = GetNameWithoutRolePrefix(txt.text);
        }

        // Check if target is a Super Admin
        bool targetIsSuperAdmin = false;
        if (idx >= 0 && idx < rowCount)
        {
            var rsc = rowScripts[idx];
            if (rsc != null) targetIsSuperAdmin = rsc.cachedIsSuperAdmin;
        }
        else
        {
            if (!string.IsNullOrEmpty(raw) && _cachedManager != null) targetIsSuperAdmin = _cachedManager.IsSuperAdmin(raw);
        }

        // Super Admin rows should not be editable by instance owners (only by Super Admins)
        if (targetIsSuperAdmin && !GetLocalIsSuperAdmin())
        {
            dj.interactable = false;
            return;
        }

        // If local player is a Super Admin or the initial owner, always allow DJ interactivity
        // regardless of target's read-only role (except for Super Admin targets handled above)
        if (GetLocalHasDjOverride())
        {
            dj.interactable = true;
            return;
        }

        // Prefer cached role index from the row script when available to avoid extra string parsing.
        int roleIdx = -1;
        if (_cachedManager != null)
        {
            if (idx >= 0 && idx < rowCount)
            {
                var rsc2 = rowScripts[idx];
                if (rsc2 != null) roleIdx = rsc2.cachedRoleIndex;
            }
            if (roleIdx < 0)
            {
                if (!string.IsNullOrEmpty(raw)) roleIdx = _cachedManager.GetRoleIndex(raw);
            }
            // If the target belongs to a role marked Read-Only, make the DJ toggle non-interactable for everyone.
            if (roleIdx >= 0 && _cachedManager.GetRoleIsReadOnly(roleIdx)) { dj.interactable = false; return; }
        }

        dj.interactable = canManageDj;
    }

    public void SetDjSystemEnabled(bool enabled)
    {
        if (_djSystemEnabled == enabled) return;
        _djSystemEnabled = enabled;
        if (Utilities.IsValid(djSystemToggle))
        {
            djSystemToggle.SetIsOnWithoutNotify(enabled);
        }
        UpdateDjToggleVisibility(_templateDjToggle, enabled);
        bool canManageDj = GetCachedManagerCanManageDj();
        for (int i = 0; i < rowCount; i++)
        {
            SetRowDjInteractable(rows[i], canManageDj, i);
        }
    }

    private void UpdateDjToggleVisibility(Toggle toggle, bool visible)
    {
        if (!Utilities.IsValid(toggle)) return;
        if (toggle.gameObject.activeSelf != visible) toggle.gameObject.SetActive(visible);
    }

    private Toggle ResolveDjToggle(VipWhitelistRow rowScript, GameObject row)
    {
        if (rowScript != null && Utilities.IsValid(rowScript.djToggle)) return rowScript.djToggle;
        return FindToggleByName(row, DJ_TOGGLE_HINTS);
    }

    private void ReleaseRowToPool(GameObject go)
    {
        // clear any per-row cached data to avoid stale references when reused
        if (!Utilities.IsValid(go)) return;
        var rowScript = go.GetComponent<VipWhitelistRow>();
        if (rowScript != null)
        {
            rowScript.parent = null;
            rowScript.nameText = null;
            rowScript.authToggle = null;
            rowScript.inWorldToggle = null;
            rowScript.djToggle = null;
            rowScript.playerId = -1;
            rowScript.cachedRawName = null;
            rowScript.cachedDisplayName = null;
            rowScript.cachedRoleIndex = -1;
            rowScript.cachedIsSuperAdmin = false;
        }
        ReleaseRow(go);
    }

    // Utility button callable from inspector to refresh list
    public void _RefreshNow()
    {
        if (!_started) return;
        RebuildPlayerList();
        if (manager != null) manager.EvaluateLocalAccess();
    }

    public bool IsAuthed(string name)
    {
        if (manager != null) return manager.IsAuthed(name);
        return false;
    }

    // Called by per-row wiring when auth toggle changes. Delegates to manager.
    public void _OnRowToggled(string playerName, bool isOn)
    {
        // Strip ALL displayed role prefixes (e.g. "(DJ) (VIP Member) Alice" -> "Alice") before forwarding
        // to the manager so stored manual entries never carry leftover role/DJ prefixes.
        // Previously used GetNameWithoutRolePrefix which only stripped a single prefix level, causing
        // names like "(VIP Member) Alice" to be stored in the manual list when both DJ and role prefixes
        // were present on the displayed name.
        string raw = NormalizeRawName(playerName);
        if (manager != null)
        {
            manager.OnRowToggled(raw, isOn);
            return;
        }

        // fallback: no manager assigned
        UpdateRowTogglesFromAuth();
    }

    public void DJToggled(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return;
        if (!_djSystemEnabled) return;
        string normalized = NormalizeRawName(playerName);
        if (string.IsNullOrEmpty(normalized)) return;
        int idx = FindRowIndexByLower(normalized);
        bool isOn = false;
        if (idx >= 0 && idx < rowCount)
        {
            var rs = rowScripts[idx];
            if (rs != null && rs.djToggle != null) isOn = rs.djToggle.isOn;
        }
        if (_cachedManager == null) return;
        if (_cachedManager.enableDebugLogs)
        {
            _cachedManager.DebugLog("DJ toggle: '" + normalized + "' isOn=" + (isOn ? "true" : "false"));
        }
        if (isOn) _cachedManager.DjAdd(normalized);
        else _cachedManager.DjRemove(normalized);
        _cachedManager.EvaluateLocalDjAccess();
        
        // Update the row but keep VIP auth toggle unchanged (DJ status should not affect VIP auth)
        // Only update display name, colors, and DJ toggle state
        if (idx >= 0 && idx < rowCount)
        {
            var rowObj = rows[idx];
            if (Utilities.IsValid(rowObj))
            {
                var rs = rowScripts[idx];
                string key = rowKeys[idx];
                
                // Update display name with DJ prefix if needed
                if (rs != null && rs.nameText != null && _cachedManager != null)
                {
                    string display = BuildDisplayName(key);
                    if (rs.nameText.text != display) rs.nameText.text = display;
                }
                
                // Update DJ toggle interactability
                SetRowDjInteractable(rowObj, GetCachedManagerCanManageDj(), idx);
            }
        }
    }

    public void DJSystemEnabled()
    {
        bool enabled = djSystemToggle != null ? djSystemToggle.isOn : !_djSystemEnabled;
        if (manager != null)
        {
            manager.SetDjSystemEnabledState(enabled);
            return;
        }
        SetDjSystemEnabled(enabled);
    }

    public void UpdateRowTogglesFromAuth()
    {
        for (int i = 0; i < rowCount; i++)
        {
            var rs = rowScripts[i];
            if (rs == null) continue;
            var rowObj = rows[i];
            if (!Utilities.IsValid(rowObj)) continue;

            string raw = rowKeys[i];
            bool authed = IsAuthed(raw);
            rs.SetAuthStateWithoutNotify(authed); // use cached row script to skip GetComponent
            bool canEdit = CanLocalEditTarget(raw);
            SetRowInteractable(rowObj, canEdit, i);
            SetRowDjInteractable(rowObj, GetCachedManagerCanManageDj(), i);
            bool isDj = _cachedManager != null ? _cachedManager.IsDj(raw) : false;
            rs.SetDjStateWithoutNotify(isDj);
            if (rs.nameText != null)
            {
                string display = BuildDisplayName(raw);
                if (rs.nameText.text != display) rs.nameText.text = display;
            }
        }
    }
}
