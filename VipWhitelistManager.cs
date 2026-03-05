using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading; // VRCStringDownloader / IVRCStringDownload
using VRC.Udon.Common.Interfaces; // IUdonEventReceiver
using System.Text;
using System;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class VipWhitelistManager : UdonSharpBehaviour
{
    [Header("List instances (already placed in scene)")]
    public VipWhitelistUI[] lists;

    // Public constants used by VipWhitelistUI
    public const int LOG_THROTTLE_SIZE = 64;
    public const int PLAYER_BUF_SIZE = 128;
    public const int MAX_ROWS = 256;
    public const int ROW_POOL_SIZE = MAX_ROWS;

    [Header("Objects to disable locally when local player is authed")]
    public GameObject[] objectsToDisableWhenAuthed;
    [Header("Objects to disable locally when local player is a DJ")]
    public GameObject[] djAreaObjects;

    [Header("Roles (editable in inspector)")]
    [Tooltip("Add or remove roles here. Each role can have a pastebin URL, a color and a manual display name.")]
    public string[] roleNames;
    public VRCUrl[] rolePastebinUrls;
    public Color[] roleColors;
    // per-role permissions
    public bool[] roleCanAddPlayers;
    public bool[] roleCanRevokePlayers;
    // per-role VIP access permission
    public bool[] roleCanVipAccess;
    // per-role DJ access permission
    public bool[] roleCanDjAccess;
    // per-role Read-Only permission: if true for a role, members of that role cannot be edited by anyone
    public bool[] roleCanReadOnly;

    [Header("Options")]
    public int maxSyncedManual = 80;
    
    [Header("Performance Caching")]
    [Tooltip("Cache size for player authentication results. Increase for events with many unique players (1000+). Higher values use more memory but improve performance. Default: 128")]
    public int accessCacheSize = 128;
    [Tooltip("Cache size for player name lookups. Increase for events with many unique players (1000+). Higher values use more memory but improve performance. Default: 128")]
    public int playerNameCacheSize = 128;
    [Tooltip("Cache size for role membership lookups. Increase if you have many roles with frequent lookups. Default: 128")]
    public int roleIndexCacheSize = 128;
    [Tooltip("Cache size for super admin checks. Usually doesn't need to be changed. Default: 128")]
    public int isSuperAdminCacheSize = 128;

    [Header("Visuals")]
    [Tooltip("Color used to display Super Admin names in UIs")]
    public Color superAdminNameColor = new Color(0.5f, 0f, 0f, 1f);

    [Tooltip("Super Admins can add/revoke anyone. Exact display names.")]
    public string[] superAdminWhitelist;

    [Header("Debug")]
    [Tooltip("Enable debug logs for the VIP Control List system.")]
    public bool enableDebugLogs = false;
    [Tooltip("Color used for debug log messages (applies to all VIP Manager logs).")]
    public Color logColor = new Color(0f, 1f, 1f, 1f);

    [UdonSynced] private string[] syncedManual = new string[80];
    [UdonSynced] private int syncedManualCount;
    [UdonSynced] private string[] syncedDj = new string[80];
    [UdonSynced] private int syncedDjCount;
    [UdonSynced] private bool syncedDjSystemEnabled = true;
    // normalized name of the initial owner who started the instance (granted DJ manage permission)
    [UdonSynced] private string initialOwner;
    // version counter incremented when parsed role lists change so clients can decide whether to rebuild
    [UdonSynced] private int roleListVersion;

    // track whether manual list or DJ system state was modified locally and needs serialization
    private bool manualDirty = false;
    private bool djSystemDirty = false;
    private const int MIN_SERIALIZATION_FRAME_INTERVAL = 10;
    private int _lastSerializationFrame = -MIN_SERIALIZATION_FRAME_INTERVAL;
    private bool _serializationRetryScheduled = false;

    private bool lastLocalAuthed;
    private bool lastLocalDj = false;
    private int _lastDjEvalFrame = -1;
    private float _lastDjLogTime = 0f;
    // Per-frame cache for local player's edit permissions (avoids O(rcount) role scan per row)
    private bool _cachedLocalIsSuperAdminVal = false;
    private bool _cachedLocalHasEditRole = false;
    private int _cachedLocalEditPermFrame = -1;
    // Per-name throttling to avoid repeated IsDj logs for the same name
    private string[] _isDjLogKeys = new string[LOG_THROTTLE_SIZE];
    private float[] _isDjLogTimes = new float[LOG_THROTTLE_SIZE];

    // Replace jagged roleMembers array with flattened single-dimensional array to avoid Udon VM object-array EXTERN issues
    // parsed role members (flattened: roleIdx * ROLE_MEMBER_CAP + memberIdx)
    private const int ROLE_MEMBER_CAP = 256;
    private string[] roleMembersFlat = new string[0];
    // normalized parallel array for roleMembersFlat to avoid repeated ToLowerInvariant/Trim calls
    private string[] roleMembersFlatNorm = new string[0];
    private int[] roleMemberCount = new int[0];

    // cache role buffers as immutable during runtime (inspector-configured only)
    private bool _roleBuffersFixed = false;
    private int _cachedRoleCount = 0;

    // cached super admin info to avoid repeated string allocations/loops
    private string[] _superAdminWhitelistNormalized = null;
    private int _superRoleIndex = -1;

    // Simple runtime cache to remember computed auth/DJ results for names during an instance.
    // When full, uses circular buffer FIFO eviction to overwrite oldest entries.
    // This ensures the cache continues to work even with many unique players over instance lifetime.
    // Size is configurable in inspector (accessCacheSize).
    private string[] _accessCacheKeys;
    private bool[] _accessCacheAuthed;
    private bool[] _accessCacheDj;
    private int _accessCacheCount = 0;
    private int _accessCacheNextEvict = 0; // circular buffer index for FIFO eviction
    private int _accessCacheVersion = 0; // incremented when cache is cleared

    // Cache for IsSuperAdmin results to avoid repeated string/role scans.
    // When full, uses circular buffer FIFO eviction to overwrite oldest entries.
    // Size is configurable in inspector (isSuperAdminCacheSize).
    private string[] _isSuperAdminCacheKeys;
    private bool[] _isSuperAdminCacheVals;
    private int _isSuperAdminCacheCount = 0;
    private int _isSuperAdminCacheNextEvict = 0; // circular buffer index for FIFO eviction
    private int _roleIndexCacheCount = 0;
    private int _roleIndexCacheNextEvict = 0; // circular buffer index for FIFO eviction

    // Cache for GetRoleIndex results to avoid repeated role member scans.
    // When full, uses circular buffer FIFO eviction to overwrite oldest entries.
    // Size is configurable in inspector (roleIndexCacheSize).
    private string[] _roleIndexCacheKeys;
    private int[] _roleIndexCacheVals;

    // Normalized caches for synced manual/DJ arrays to avoid repeated normalization per-compare
    private string[] syncedManualNorm = null;
    private string[] syncedDjNorm = null;

    // Player ID to normalized name cache to avoid repeated player.displayName access and normalization.
    // When full, uses circular buffer FIFO eviction to overwrite oldest entries.
    // This ensures new players can always be cached even if many unique players join over instance lifetime.
    // Size is configurable in inspector (playerNameCacheSize).
    private int[] _playerIdCache;
    private string[] _playerNameCache;
    private string[] _playerDisplayNameCache; // Original displayName for logging
    private int _playerNameCacheCount = 0;
    private int _playerNameCacheNextEvict = 0; // circular buffer index for FIFO eviction
    // Version counter for player name cache, incremented when cache is cleared
    private int _playerNameCacheVersion = 0;

    private int RoleMemberOffset(int roleIdx)
    {
        return roleIdx * ROLE_MEMBER_CAP;
    }

    // Access cache helpers
    private void ClearAccessCache()
    {
        _accessCacheCount = 0;
        _accessCacheNextEvict = 0;
        _accessCacheVersion++;
        // also clear auxiliary caches
        _isSuperAdminCacheCount = 0;
        _isSuperAdminCacheNextEvict = 0;
        _roleIndexCacheCount = 0;
        _roleIndexCacheNextEvict = 0;
        // also clear player name cache since role membership may have changed
        _playerNameCacheCount = 0;
        _playerNameCacheNextEvict = 0;
        _playerNameCacheVersion++;
    }

    // Ensure the access cache contains an entry for the normalized key and returns its index.
    // If missing, compute both authed and dj and store in cache.
    private int EnsureAccessCached(string normalizedKey)
    {
        if (string.IsNullOrEmpty(normalizedKey)) return -1;
        
        // Search existing cache (only if cache is initialized)
        if (_accessCacheKeys != null)
        {
            int cacheSize = _accessCacheCount;
            for (int i = 0; i < cacheSize; i++)
            {
                if (_accessCacheKeys[i] == normalizedKey) return i;
            }
        }
        
        // Cache miss - compute values
        bool authed = false;
        bool dj = false;

        // Compute authed status
        // Check superadmin first (highest priority)
        if (IsSuperAdmin(normalizedKey))
        {
            authed = true;
        }
        // Check manual list
        else if (ArrayContains(syncedManual, syncedManualCount, normalizedKey))
        {
            authed = true;
        }
        // Check role VIP access
        else
        {
            int rcount = _cachedRoleCount;
            for (int ri = 0; ri < rcount; ri++)
            {
                bool vipPerm = roleCanVipAccess != null && ri < roleCanVipAccess.Length && roleCanVipAccess[ri];
                if (!vipPerm) continue;
                
                int cnt = GetRoleMemberCountPublic(ri);
                if (RoleArrayContains(ri, cnt, normalizedKey))
                {
                    authed = true;
                    break;
                }
            }
        }

        // Compute DJ status
        if (ArrayContains(syncedDj, syncedDjCount, normalizedKey))
        {
            dj = true;
        }
        else
        {
            int rcount = _cachedRoleCount;
            for (int ri = 0; ri < rcount; ri++)
            {
                bool djPerm = roleCanDjAccess != null && ri < roleCanDjAccess.Length && roleCanDjAccess[ri];
                if (!djPerm) continue;
                
                int cnt = GetRoleMemberCountPublic(ri);
                if (RoleArrayContains(ri, cnt, normalizedKey))
                {
                    dj = true;
                    break;
                }
            }
        }

        // Store in cache (only if cache is initialized)
        if (_accessCacheKeys != null && _accessCacheAuthed != null && _accessCacheDj != null)
        {
            int cacheLen = _accessCacheKeys.Length;
            int storeIdx;
            if (_accessCacheCount < cacheLen)
            {
                // Cache not full yet - append new entry
                storeIdx = _accessCacheCount++;
            }
            else
            {
                // Cache full - evict oldest entry using circular buffer
                storeIdx = _accessCacheNextEvict;
                _accessCacheNextEvict = (_accessCacheNextEvict + 1) % cacheLen;
                if (enableDebugLogs)
                {
                    string evictedKey = _accessCacheKeys[storeIdx];
                    if (!string.IsNullOrEmpty(evictedKey))
                    {
                        DebugLog("Access cache full (" + cacheLen.ToString() + " entries) - evicting oldest entry: '" + evictedKey + "'");
                    }
                }
            }
            _accessCacheKeys[storeIdx] = normalizedKey;
            _accessCacheAuthed[storeIdx] = authed;
            _accessCacheDj[storeIdx] = dj;
            return storeIdx;
        }
        
        // If cache not initialized, return -1 (caller should handle gracefully)
        return -1;
    }

    // Public helper so UI can ensure manager role buffers are initialized before querying role data.
    public void EnsureRoleBuffersInitialized()
    {
        EnsureRoleBuffersOnce();
    }

    // Optimized role array contains check
    private bool RoleArrayContains(int roleIdx, int count, string value)
    {
        if (roleMembersFlat == null || count <= 0 || string.IsNullOrEmpty(value)) return false;
        
        int offset = RoleMemberOffset(roleIdx);
        int maxCount = Mathf.Min(count, ROLE_MEMBER_CAP);
        
        // Use precomputed normalized parallel array for faster case-insensitive comparisons
        if (roleMembersFlatNorm != null && roleMembersFlatNorm.Length >= offset + maxCount)
        {
            string targetNorm = NormalizeForCompare(value);
            if (string.IsNullOrEmpty(targetNorm)) return false;
            
            int roleMembersLen = roleMembersFlatNorm.Length;
            for (int j = 0; j < maxCount; j++)
            {
                int idx = offset + j;
                if (idx >= roleMembersLen) break;
                var vn = roleMembersFlatNorm[idx];
                if (string.IsNullOrEmpty(vn)) continue;
                if (vn == targetNorm) return true;
            }
        }
        else
        {
            // Fallback to original behavior if normalized buffer missing
            string target = NormalizeForCompare(value);
            if (string.IsNullOrEmpty(target)) return false;
            
            int roleMembersLen = roleMembersFlat.Length;
            for (int j = 0; j < maxCount; j++)
            {
                int idx = offset + j;
                if (idx >= roleMembersLen) break;
                var v = roleMembersFlat[idx];
                if (string.IsNullOrEmpty(v)) continue;
                if (NormalizeForCompare(v) == target) return true;
            }
        }
        return false;
    }

    // Public accessor to check if a given player is the recorded initial owner of the instance.
    public bool IsInstanceInitialOwner(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return false;
        if (string.IsNullOrEmpty(initialOwner)) return false;
        string local = NormalizeNameFromPlayer(player);
        if (string.IsNullOrEmpty(local)) return false;
        return initialOwner == local;
    }

    // Overload: check if a name is the initial owner
    public bool IsInstanceInitialOwner(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (string.IsNullOrEmpty(initialOwner)) return false;
        string normalized = NormalizeName(StripRolePrefix(name));
        if (string.IsNullOrEmpty(normalized)) return false;
        return initialOwner == normalized;
    }

    // Ensure role member buffers sized to inspector role count. This method is still safe to call multiple times
    // but does not perform repeated allocation/resizing for the inspector-only permission arrays (they are initialized once).
    private void EnsureRoleBuffers()
    {
        int rcount = GetRoleCount();
        if (rcount <= 0)
        {
            // Ensure arrays exist as empty to avoid null references
            roleMembersFlat = new string[0];
            roleMembersFlatNorm = new string[0];
            roleMemberCount = new int[0];
            return;
        }

        int expectedFlatLen = rcount * ROLE_MEMBER_CAP;
        bool needsResize = (roleMembersFlat == null) || 
                           (roleMemberCount == null) || 
                           (roleMemberCount.Length != rcount) || 
                           (roleMembersFlat.Length != expectedFlatLen);
        
        if (needsResize)
        {
            var newFlat = new string[expectedFlatLen];
            var newFlatNorm = new string[expectedFlatLen];
            var newCounts = new int[rcount];
            
            // Copy existing data if present
            if (roleMemberCount != null && roleMembersFlat != null && roleMembersFlat.Length > 0)
            {
                int oldRoleCount = roleMemberCount.Length;
                int copyRoleCount = Mathf.Min(oldRoleCount, rcount);
                
                for (int i = 0; i < copyRoleCount; i++)
                {
                    int oldOffset = i * ROLE_MEMBER_CAP;
                    int newOffset = i * ROLE_MEMBER_CAP;
                    int copyCount = Mathf.Min(ROLE_MEMBER_CAP, roleMemberCount[i]);
                    
                    // Copy member names
                    for (int j = 0; j < copyCount; j++)
                    {
                        newFlat[newOffset + j] = roleMembersFlat[oldOffset + j];
                    }
                    
                    newCounts[i] = copyCount;
                    
                    // Copy normalized values if present
                    if (roleMembersFlatNorm != null && oldOffset < roleMembersFlatNorm.Length)
                    {
                        int normCopyCount = Mathf.Min(copyCount, roleMembersFlatNorm.Length - oldOffset);
                        for (int j = 0; j < normCopyCount; j++)
                        {
                            newFlatNorm[newOffset + j] = roleMembersFlatNorm[oldOffset + j];
                        }
                    }
                }
            }
            
            roleMembersFlat = newFlat;
            roleMembersFlatNorm = newFlatNorm;
            roleMemberCount = newCounts;
        }
    }

    // Initialize permission/color/url/name arrays once at startup and treat as static during runtime.
    private void InitializeRolePermissionArrays(int rcount)
    {
        if (rcount <= 0) return;

        roleNames = ResizeStringArray(roleNames, rcount);
        rolePastebinUrls = ResizeVRCUrlArray(rolePastebinUrls, rcount);
        roleColors = ResizeColorArray(roleColors, rcount);
        roleCanAddPlayers = ResizeBoolArray(roleCanAddPlayers, rcount);
        roleCanRevokePlayers = ResizeBoolArray(roleCanRevokePlayers, rcount);
        roleCanVipAccess = ResizeBoolArray(roleCanVipAccess, rcount);
        roleCanDjAccess = ResizeBoolArray(roleCanDjAccess, rcount);
        roleCanReadOnly = ResizeBoolArray(roleCanReadOnly, rcount);
    }

    // Array resize helpers - UdonSharp doesn't support generics, so we need typed methods
    // Each method follows the same pattern: check size, allocate new array, copy data, return result
    private string[] ResizeStringArray(string[] src, int rcount)
    {
        if (src != null && src.Length == rcount) return src;
        var tmp = new string[rcount];
        if (src != null)
        {
            int copyCount = Mathf.Min(src.Length, rcount);
            for (int i = 0; i < copyCount; i++) tmp[i] = src[i];
        }
        return tmp;
    }

    private VRCUrl[] ResizeVRCUrlArray(VRCUrl[] src, int rcount)
    {
        if (src != null && src.Length == rcount) return src;
        var tmp = new VRCUrl[rcount];
        if (src != null)
        {
            int copyCount = Mathf.Min(src.Length, rcount);
            for (int i = 0; i < copyCount; i++) tmp[i] = src[i];
        }
        return tmp;
    }

    private Color[] ResizeColorArray(Color[] src, int rcount)
    {
        if (src != null && src.Length == rcount) return src;
        var tmp = new Color[rcount];
        int tmpLen = tmp.Length;
        if (src != null)
        {
            int copyCount = Mathf.Min(src.Length, rcount);
            for (int i = 0; i < copyCount; i++) tmp[i] = src[i];
        }
        else
        {
            // Initialize with default color only when source is null
            for (int i = 0; i < tmpLen; i++) tmp[i] = Color.white;
        }
        return tmp;
    }

    private bool[] ResizeBoolArray(bool[] src, int rcount)
    {
        if (src != null && src.Length == rcount) return src;
        var tmp = new bool[rcount];
        if (src != null)
        {
            int copyCount = Mathf.Min(src.Length, rcount);
            for (int i = 0; i < copyCount; i++) tmp[i] = src[i];
        }
        return tmp;
    }

    // single-call initializer for role buffers (optimized because inspector configuration is static at runtime)
    private void EnsureRoleBuffersOnce()
    {
        if (_roleBuffersFixed) return;
        EnsureRoleBuffers();
        int rcount = GetRoleCount();
        InitializeRolePermissionArrays(rcount);
        _cachedRoleCount = rcount;

        // prepare normalized super admin whitelist for fast comparisons
        if (superAdminWhitelist != null)
        {
            _superAdminWhitelistNormalized = new string[superAdminWhitelist.Length];
            for (int i = 0; i < superAdminWhitelist.Length; i++)
            {
                string v = superAdminWhitelist[i];
                if (string.IsNullOrEmpty(v)) { _superAdminWhitelistNormalized[i] = null; continue; }
                string n = NormalizeName(StripRolePrefix(v)).ToLowerInvariant();
                _superAdminWhitelistNormalized[i] = n;
            }
        }
        else
        {
            _superAdminWhitelistNormalized = null;
        }

        // cache index of any role matching "Super Admin" (case-insensitive contains)
        _superRoleIndex = GetRoleIndexByRoleName("Super Admin");

        _roleBuffersFixed = true;
    }

    // Debug helper: set a simple runtime stage into inspector-visible string (safe, no arrays created)
    private void SetDebugStage(string s)
    {
        // no-op: inspector-visible debug field removed
    }

    // Cached hex string for logColor to avoid per-call string allocations in the debug log methods.
    // Lazily computed on first debug log call; reset in Start() to pick up any inspector changes.
    private string _cachedLogColorHex = null;

    // Centralized debug logger that respects the enableDebugLogs flag and the configured logColor.
    public void DebugLog(string msg)
    {
        if (!enableDebugLogs) return;
        if (_cachedLogColorHex == null) _cachedLogColorHex = ColorToHex(logColor);
        Debug.Log("<color=#" + _cachedLogColorHex + ">(VIP Manager) " + msg + "</color>");
    }
    public void DebugLogWarning(string msg)
    {
        if (!enableDebugLogs) return;
        if (_cachedLogColorHex == null) _cachedLogColorHex = ColorToHex(logColor);
        Debug.LogWarning("<color=#" + _cachedLogColorHex + ">(VIP Manager) " + msg + "</color>");
    }
    public void DebugLogError(string msg)
    {
        if (!enableDebugLogs) return;
        if (_cachedLogColorHex == null) _cachedLogColorHex = ColorToHex(logColor);
        Debug.LogError("<color=#" + _cachedLogColorHex + ">(VIP Manager) " + msg + "</color>");
    }

    // Convert a Unity Color to an 8-character hex string (RRGGBBAA) without using ColorUtility
    private string ColorToHex(Color c)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
        int a = Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
        return IntToHex2(r) + IntToHex2(g) + IntToHex2(b) + IntToHex2(a);
    }

    private string IntToHex2(int v)
    {
        const string hexDigits = "0123456789ABCDEF";
        int hi = (v >> 4) & 0xF;
        int lo = v & 0xF;
        char hiC = hexDigits[hi];
        char loC = hexDigits[lo];
        return "" + hiC + loC;
    }

    // Cached LocalPlayer lookup per-frame to avoid repeated property access overhead
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

    // Log roles and permissions into inspector-accessible string to avoid Udon VM object-array issues
    void Start()
    {
        // Always log startup to confirm script is running
        Debug.Log("(VIP Manager) Start() called. enableDebugLogs=" + enableDebugLogs.ToString());
        
        SetDebugStage("Start:entered");
        
        // Initialize runtime caches with inspector-configured sizes
        // Clamp to reasonable ranges to prevent memory issues
        int accessSize = Mathf.Clamp(accessCacheSize, 32, 2048);
        int playerNameSize = Mathf.Clamp(playerNameCacheSize, 32, 2048);
        int roleIndexSize = Mathf.Clamp(roleIndexCacheSize, 32, 2048);
        int superAdminSize = Mathf.Clamp(isSuperAdminCacheSize, 32, 2048);
        
        _accessCacheKeys = new string[accessSize];
        _accessCacheAuthed = new bool[accessSize];
        _accessCacheDj = new bool[accessSize];
        
        _playerIdCache = new int[playerNameSize];
        _playerNameCache = new string[playerNameSize];
        _playerDisplayNameCache = new string[playerNameSize];
        
        _roleIndexCacheKeys = new string[roleIndexSize];
        _roleIndexCacheVals = new int[roleIndexSize];
        
        _isSuperAdminCacheKeys = new string[superAdminSize];
        _isSuperAdminCacheVals = new bool[superAdminSize];
        
        if (enableDebugLogs)
        {
            DebugLog("Cache sizes initialized: Access=" + accessSize.ToString() + 
                     ", PlayerName=" + playerNameSize.ToString() + 
                     ", RoleIndex=" + roleIndexSize.ToString() + 
                     ", SuperAdmin=" + superAdminSize.ToString());
        }
        
        EnsureSyncedArrayCapacity();
        SetDebugStage("Start:afterEnsureSynced");
        CompactSyncedManual();
        SetDebugStage("Start:afterCompactSynced");

        // compact DJ list as well
        CompactSyncedDj();

        // initialize role member buffers to match inspector roles
        EnsureRoleBuffersOnce();
        SetDebugStage("Start:afterEnsureRoleBuffers");
        
        if (enableDebugLogs)
        {
            if (superAdminWhitelist != null && superAdminWhitelist.Length > 0)
            {
                DebugLog("Initialized " + superAdminWhitelist.Length.ToString() + " super admin(s)");
            }
            
            DebugLog("Start: Role count = " + _cachedRoleCount.ToString());
        }

        SetDebugStage("Start:beforeInitLists");
        // apply current DJ system state to all inspector-assigned lists
        if (lists != null)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                if (lists[i] != null) ApplyDjSystemStateToList(lists[i]);
            }
        }
        SetDebugStage("Start:afterInitLists");

        // Load all role URLs if specified
        int rcount = _cachedRoleCount;
        if (rolePastebinUrls != null && rcount > 0)
        {
            // Cache a single UdonBehaviour receiver instance to avoid repeated GetComponent calls in the loop
            var udonBeh = (VRC.Udon.UdonBehaviour)gameObject.GetComponent(typeof(VRC.Udon.UdonBehaviour));
            
            if (enableDebugLogs && udonBeh != null)
            {
                DebugLog("Loading " + rcount.ToString() + " role URL(s)...");
            }
            
            for (int i = 0; i < rcount; i++)
            {
                if (rolePastebinUrls.Length <= i) continue;
                var urlObj = rolePastebinUrls[i];
                if (urlObj != null)
                {
                    string rUrl = urlObj.Get();
                    if (!string.IsNullOrEmpty(rUrl))
                    {
                        if (udonBeh != null)
                        {
                            if (enableDebugLogs)
                            {
                                string roleName = (roleNames != null && i < roleNames.Length && !string.IsNullOrEmpty(roleNames[i])) 
                                    ? roleNames[i] 
                                    : ("role_" + i.ToString());
                                DebugLog("Loading URL for role '" + roleName + "': " + rUrl);
                            }
                            VRCStringDownloader.LoadUrl(urlObj, (VRC.Udon.Common.Interfaces.IUdonEventReceiver)udonBeh);
                        }
                        else
                        {
                            // fallback: skip loading to avoid VM EXTERN null reference
                            SetDebugStage("Start:skipLoadUrl:noUdonBehaviour");
                        }
                    }
                }
            }
        }
        SetDebugStage("Start:afterLoadUrls");

        // Do NOT automatically take ownership on Start to avoid unnecessary ownership churn.
        // Ownership is requested only when a local user attempts to modify the manual list.
        SetDebugStage("Start:afterSetOwner");

        // initial evaluation of local access
        EvaluateLocalAccess();
        SetDebugStage("Start:done");

        // If this instance hasn't recorded an initial owner yet, the very first
        // owner who runs Start() should be stored and granted DJ management permission.
        // Only the current owner can write synced data.
        if (string.IsNullOrEmpty(initialOwner))
        {
            VRCPlayerApi lp = GetLocalPlayer();
            if (Utilities.IsValid(lp) && Networking.IsOwner(gameObject))
            {
                initialOwner = NormalizeNameFromPlayer(lp);
                if (!Networking.IsClogged)
                {
                    RequestSerialization();
                }
            }
        }
        
        Debug.Log("(VIP Manager) Start() completed successfully");
    }

    // expose read-only flag accessor
    public bool GetRoleIsReadOnly(int idx)
    {
        if (roleCanReadOnly == null || idx < 0 || idx >= roleCanReadOnly.Length) return false;
        return roleCanReadOnly[idx];
    }

    // determine if a target name is locked against edits due to being in a read-only role
    private bool IsTargetReadOnly(string targetName)
    {
        if (string.IsNullOrEmpty(targetName)) return false;
        int ridx = GetRoleIndex(targetName);
        if (ridx >= 0)
        {
            if (GetRoleIsReadOnly(ridx)) return true;
        }
        return false;
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        // Called when a single player joins the instance.
        // Notify each registered UI to handle that specific player incrementally (UI will add or update a single row).
        // This avoids performing a full list rebuild on every join.
        if (lists != null)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                var ui = lists[i];
                if (ui == null) continue;
                ui.OnPlayerJoined(player);
            }
        }

        // Recompute local player's access because role membership can be dynamic and influenced by the joining player.
        EvaluateLocalAccess();

        // Notify UIs to update the joining player's row auth state/color based on current manual/role lists.
        if (Utilities.IsValid(player))
        {
            string raw = NormalizeNameFromPlayer(player);
            if (!string.IsNullOrEmpty(raw)) NotifyListsForName(raw, IsPlayerAuthorized(player));
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        // Called when a single player leaves the instance.
        // Notify each registered UI so it can mark the row not-in-world or remove it if appropriate.
        if (lists != null)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                var ui = lists[i];
                if (ui == null) continue;
                ui.OnPlayerLeft(player);
            }
        }

        // Remove from player name cache to avoid stale entries
        if (Utilities.IsValid(player))
        {
            int playerId = player.playerId;
            if (playerId >= 0)
            {
                for (int i = 0; i < _playerNameCacheCount; i++)
                {
                    if (_playerIdCache[i] == playerId)
                    {
                        // Shift remaining entries down in all three parallel arrays
                        for (int j = i + 1; j < _playerNameCacheCount; j++)
                        {
                            _playerIdCache[j - 1] = _playerIdCache[j];
                            _playerNameCache[j - 1] = _playerNameCache[j];
                            _playerDisplayNameCache[j - 1] = _playerDisplayNameCache[j];
                        }
                        _playerNameCacheCount--;
                        break;
                    }
                }
            }
        }

        // Recompute local player's access because leaving players can affect dynamic role membership.
        EvaluateLocalAccess();
    }

    public override void OnDeserialization()
    {
        // Called when synced state variables (manual/DJ arrays, counts, etc.) are deserialized from the network.
        // Ensure local array capacities match the configured max, remove empty/duplicate entries via compaction,
        // clear any runtime caches that depend on synced lists, and notify UIs to refresh.
        if (enableDebugLogs)
        {
            DebugLog("OnDeserialization: received synced manual/DJ lists. Count=" + syncedManualCount.ToString());
        }
        EnsureSyncedArrayCapacity();
        CompactSyncedManual();
        CompactSyncedDj();
        // Synced lists changed -> invalidate runtime access cache (IsAuthed/IsDj results cached in memory)
        ClearAccessCache();
        // Notify UIs to update rows (either lightweight updates or full rebuild depending on role/manual version)
        NotifyLists();
        // Force re-evaluation of local access/UI objects since synced lists may change interactivity or local object states
        EvaluateLocalAccess(true);
        EvaluateLocalDjAccess();
        BroadcastDjSystemState();
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        SetDebugStage("OnStringLoadSuccess:entered");
        string text = result != null ? result.Result : null;
        // Attempt to determine which URL this result corresponds to
        string srcUrl = null;
        if (result != null && result.Url != null)
        {
            srcUrl = result.Url.Get();
        }
        SetDebugStage("OnStringLoadSuccess:afterGetUrl");

        bool handled = false;
        // try to match against roles' URLs
        int rcount = _roleBuffersFixed ? _cachedRoleCount : (roleNames != null ? roleNames.Length : 0);
        if (!string.IsNullOrEmpty(srcUrl) && rolePastebinUrls != null)
        {
            // Attempt to match the downloaded string to a configured role URL and parse that role's members.
            for (int i = 0; i < rcount; i++)
            {
                SetDebugStage("OnStringLoadSuccess:checkingRole:" + i.ToString());
                var url = rolePastebinUrls.Length > i ? rolePastebinUrls[i] : null;
                if (url == null) continue;
                if (srcUrl == url.Get())
                {
                    SetDebugStage("OnStringLoadSuccess:match:" + i.ToString());
                    if (enableDebugLogs)
                    {
                        string roleName = (roleNames != null && i < roleNames.Length && !string.IsNullOrEmpty(roleNames[i])) 
                            ? roleNames[i] 
                            : ("role_" + i.ToString());
                        DebugLog("Matched URL to role '" + roleName + "', parsing...");
                    }
                    ParseRole(text, i);
                    handled = true;
                    // Increment version so NotifyLists forces a full rebuild on all clients
                    if (Networking.IsOwner(gameObject))
                    {
                        roleListVersion++;
                        manualDirty = true;
                    }
                    break;
                }
            }
        }

        if (!handled && enableDebugLogs)
        {
            // Not a managed role URL; ignore
            DebugLog("Received string load for unrecognized URL: " + (srcUrl ?? "(null)"));
        }

        NotifyLists();
        EvaluateLocalAccess();
        SetDebugStage("OnStringLoadSuccess:done");
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        SetDebugStage("OnStringLoadError:entered");
        
        string srcUrl = null;
        string errorMsg = "Unknown error";
        
        if (result != null)
        {
            if (result.Url != null)
            {
                srcUrl = result.Url.Get();
            }
            errorMsg = result.ErrorCode.ToString();
        }
        
        // Try to identify which role URL failed
        string roleName = "Unknown";
        if (!string.IsNullOrEmpty(srcUrl) && rolePastebinUrls != null)
        {
            int rcount = _roleBuffersFixed ? _cachedRoleCount : (roleNames != null ? roleNames.Length : 0);
            for (int i = 0; i < rcount; i++)
            {
                var url = rolePastebinUrls.Length > i ? rolePastebinUrls[i] : null;
                if (url != null && srcUrl == url.Get())
                {
                    roleName = (roleNames != null && i < roleNames.Length && !string.IsNullOrEmpty(roleNames[i])) 
                        ? roleNames[i] 
                        : ("role_" + i.ToString());
                    break;
                }
            }
        }
        
        // Always log errors regardless of debug setting so users can troubleshoot
        string msg = "Failed to load role list for '" + roleName + "'. URL: " + (srcUrl ?? "(null)") + ", Error: " + errorMsg;
        DebugLog(msg);
        Debug.LogError("(VIP Manager) " + msg);
        
        SetDebugStage("OnStringLoadError:done");
    }

    // Optimized role parsing with better bounds checking
    private void ParseRole(string text, int roleIndex)
    {
        SetDebugStage("ParseRole:entered:" + roleIndex.ToString());
        EnsureRoleBuffers();
        SetDebugStage("ParseRole:afterEnsureRoleBuffers:" + roleIndex.ToString());
        
        int roleNameCount = roleNames != null ? roleNames.Length : 0;
        if (roleIndex < 0 || roleIndex >= roleNameCount)
        {
            SetDebugStage("ParseRole:outOfRangeIndex:" + roleIndex.ToString());
            return;
        }
        
        roleMemberCount[roleIndex] = 0;
        
        // Clear normalized buffer for this role if present
        if (roleMembersFlatNorm != null)
        {
            int normOffset = RoleMemberOffset(roleIndex);
            int normLen = roleMembersFlatNorm.Length;
            int clearEnd = Mathf.Min(normOffset + ROLE_MEMBER_CAP, normLen);
            for (int i = normOffset; i < clearEnd; i++)
            {
                roleMembersFlatNorm[i] = null;
            }
        }
        
        SetDebugStage("ParseRole:clearedCount:" + roleIndex.ToString());
        
        if (text == null)
        {
            SetDebugStage("ParseRole:textNull:" + roleIndex.ToString());
            return;
        }
        
        // Accept newline-separated or comma-separated lists. Normalize CR and split on newline or comma.
        string cleaned = text.Replace("\r", "");
        char[] sep = new char[] { '\n', ',' };
        string[] tokens = cleaned.Split(sep);
        int offset = RoleMemberOffset(roleIndex);

        // Collect added members for debug logging (only if debug enabled)
        System.Text.StringBuilder addedSb = enableDebugLogs ? new System.Text.StringBuilder() : null;
        int addedCount = 0;

        // Cap tokens processed to avoid long blocking parsing in large files
        const int MAX_PARSE_TOKENS = 1000;
        int tokensLen = tokens.Length;
        int maxTokensToProcess = Mathf.Min(tokensLen, MAX_PARSE_TOKENS);
        bool truncated = tokensLen > MAX_PARSE_TOKENS;

        int currentMemberCount = 0; // Local counter to avoid repeated array access
        
        for (int i = 0; i < maxTokensToProcess; i++)
        {
            SetDebugStage("ParseRole:token:" + roleIndex.ToString() + ":" + i.ToString());
            
            if (currentMemberCount >= ROLE_MEMBER_CAP) break;
            
            string line = tokens[i];
            if (string.IsNullOrEmpty(line)) continue;
            
            line = line.Trim();
            if (line.Length == 0) continue;
            
            string exact = line;
            if (!RoleArrayContains(roleIndex, currentMemberCount, exact))
            {
                int pos = offset + currentMemberCount;
                roleMembersFlat[pos] = exact;
                
                if (roleMembersFlatNorm != null && pos < roleMembersFlatNorm.Length)
                {
                    roleMembersFlatNorm[pos] = NormalizeForCompare(exact);
                }
                
                currentMemberCount++;
                roleMemberCount[roleIndex] = currentMemberCount;
                
                // Append to debug list (bounded to avoid huge debug allocations)
                if (addedSb != null)
                {
                    if (addedCount > 0) addedSb.Append(", ");
                    addedSb.Append(exact);
                }
                addedCount++;
            }
        }

        // Log parsed members and role VIP permission
        if (enableDebugLogs)
        {
            string roleName = (roleNames != null && roleIndex >= 0 && roleIndex < roleNames.Length) 
                ? roleNames[roleIndex] 
                : ("role_" + roleIndex.ToString());
            
            bool vipPerm = (roleCanVipAccess != null && roleIndex >= 0 && roleIndex < roleCanVipAccess.Length) 
                && roleCanVipAccess[roleIndex];
            
            string msg = "ParseRole: role '" + roleName + "' parsed " + addedCount.ToString() + 
                      " members. VIP permission: " + (vipPerm ? "YES" : "NO");
            
            if (addedCount > 0 && addedSb != null)
            {
                string addedStr = addedSb.ToString();
                if (truncated) addedStr += " ...(truncated)";
                
                // Cap debug message length
                const int MAX_DEBUG_LEN = 200;
                if (addedStr.Length > MAX_DEBUG_LEN)
                {
                    addedStr = addedStr.Substring(0, MAX_DEBUG_LEN) + " ...";
                }
                
                msg += ". Members: " + addedStr;
            }
            
            DebugLog(msg);
        }
        
        SetDebugStage("ParseRole:done:" + roleIndex.ToString());
        
        // Role membership changed -> clear cached access results
        ClearAccessCache();
    }

    private int GetEffectiveMaxSyncedManual()
    {
        int m = maxSyncedManual;
        if (m <= 0) m = 80;
        if (m > 100) m = 100;
        return m;
    }

    // Use exact trimmed names only
    private string NormalizeName(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Trim();
    }

    // Strip a leading role prefix in parentheses from a displayed name, e.g. "(Role) Alice" -> "Alice"
    private string StripRolePrefix(string displayed)
    {
        if (string.IsNullOrEmpty(displayed)) return displayed;
        string t = displayed.Trim();
        // Remove repeated leading parenthesized prefixes, e.g. "(DJ) (Role) Alice" -> "Alice"
        while (t.Length > 0 && t[0] == '(')
        {
            int idx = t.IndexOf(')');
            if (idx <= 0) break;
            if (idx + 1 >= t.Length) { t = ""; break; }
            t = t.Substring(idx + 1).Trim();
        }
        return t;
    }

    // Normalize and strip role prefix from a VRC player display name
    private string NormalizeNameFromPlayer(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return null;
        // Check cache first using player ID (only if cache is initialized)
        int playerId = player.playerId;
        if (playerId >= 0 && _playerIdCache != null && _playerNameCache != null)
        {
            for (int i = 0; i < _playerNameCacheCount; i++)
            {
                if (_playerIdCache[i] == playerId)
                {
                    return _playerNameCache[i];
                }
            }
        }
        // Cache miss - compute and store
        string displayName = player.displayName;
        string result = NormalizeName(StripRolePrefix(displayName));
        // Store in cache using circular buffer with proper FIFO eviction when full (only if cache is initialized)
        if (playerId >= 0 && _playerIdCache != null && _playerNameCache != null && _playerDisplayNameCache != null)
        {
            int cacheLen = _playerNameCache.Length;
            int idx;
            if (_playerNameCacheCount < cacheLen)
            {
                // Cache not full yet - append new entry
                idx = _playerNameCacheCount++;
            }
            else
            {
                // Cache full - evict oldest entry using circular buffer
                idx = _playerNameCacheNextEvict;
                _playerNameCacheNextEvict = (_playerNameCacheNextEvict + 1) % cacheLen;
                if (enableDebugLogs)
                {
                    int evictedId = _playerIdCache[idx];
                    string evictedName = _playerDisplayNameCache[idx];
                    if (!string.IsNullOrEmpty(evictedName))
                    {
                        DebugLog("Player name cache full (" + cacheLen.ToString() + " entries) - evicting oldest entry: ID=" + evictedId.ToString() + " name='" + evictedName + "'");
                    }
                }
            }
            _playerIdCache[idx] = playerId;
            _playerNameCache[idx] = result;
            _playerDisplayNameCache[idx] = displayName; // Store original for logging
        }
        return result;
    }

    // Get cached original displayName for a player (for logging/display purposes)
    private string GetCachedDisplayName(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return null;
        int playerId = player.playerId;
        if (playerId >= 0 && _playerIdCache != null && _playerDisplayNameCache != null)
        {
            for (int i = 0; i < _playerNameCacheCount; i++)
            {
                if (_playerIdCache[i] == playerId)
                {
                    return _playerDisplayNameCache[i];
                }
            }
        }
        // Cache miss - ensure it's cached and return
        NormalizeNameFromPlayer(player);
        // Try again after caching
        if (playerId >= 0 && _playerIdCache != null && _playerDisplayNameCache != null)
        {
            for (int i = 0; i < _playerNameCacheCount; i++)
            {
                if (_playerIdCache[i] == playerId)
                {
                    return _playerDisplayNameCache[i];
                }
            }
        }
        return player.displayName;
    }

    // Normalize a string for case-insensitive comparisons (trim + lowercase)
    private string NormalizeForCompare(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Trim().ToLowerInvariant();
    }

    // Overload: get role index using a VRC player
    private int GetRoleIndex(VRCPlayerApi player)
    {
        string name = NormalizeNameFromPlayer(player);
        if (string.IsNullOrEmpty(name)) return -1;
        return GetRoleIndex(name);
    }

    // Overload: check superadmin status using VRC player
    private bool IsSuperAdmin(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return false;
        // Use cached displayName (not normalized) so IsSuperAdmin(string) can detect role prefixes
        string displayName = GetCachedDisplayName(player);
        if (string.IsNullOrEmpty(displayName)) return false;
        return IsSuperAdmin(displayName);
    }

    // Overload: check auth using VRC player
    private bool IsPlayerAuthorized(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return false;
        // prefer the name-stripping path so manual/role lookup remains consistent
        string exact = NormalizeNameFromPlayer(player);
        if (string.IsNullOrEmpty(exact)) return false;
        return IsAuthed(exact);
    }

    private void EnsureSyncedArrayCapacity()
    {
        int target = GetEffectiveMaxSyncedManual();
        if (syncedManual == null || syncedManual.Length != target)
        {
            string[] newArr = new string[target];
            syncedManualNorm = new string[target];
            for (int i = 0; i < syncedManualCount; i++)
            {
                if (i >= target) break;
                newArr[i] = syncedManual[i];
                if (!string.IsNullOrEmpty(newArr[i])) syncedManualNorm[i] = NormalizeForCompare(newArr[i]);
            }
            syncedManual = newArr;
            if (syncedManualCount > target) syncedManualCount = target;
        }
        // ensure DJ synced array has same capacity
        if (syncedDj == null || syncedDj.Length != target)
        {
            string[] newArr2 = new string[target];
            syncedDjNorm = new string[target];
            if (syncedDj != null)
            {
                for (int i = 0; i < syncedDjCount && i < target; i++) newArr2[i] = syncedDj[i];
                for (int i = 0; i < syncedDjCount && i < target; i++) if (!string.IsNullOrEmpty(newArr2[i])) syncedDjNorm[i] = NormalizeForCompare(newArr2[i]);
            }
            syncedDj = newArr2;
            if (syncedDjCount > target) syncedDjCount = target;
        }
    }

    // Optimized compaction with reduced redundant operations
    private void CompactSyncedManual()
    {
        if (syncedManual == null) return;
        
        int write = 0;
        int syncedManualLen = syncedManual.Length;
        int maxIndex = Mathf.Min(syncedManualCount, syncedManualLen);
        
        for (int i = 0; i < maxIndex; i++)
        {
            string s = syncedManual[i];
            if (string.IsNullOrEmpty(s)) continue;
            
            string exact = NormalizeName(s);
            if (string.IsNullOrEmpty(exact)) continue;
            
            // Check for duplicates using the already-written normalized entries for case-insensitive
            // dedup. Previously used case-sensitive == on the raw array, allowing "Alice"/"alice"
            // variants to both survive compaction.
            string exactNorm = NormalizeForCompare(exact);
            bool isDup = false;
            for (int j = 0; j < write; j++)
            {
                if (syncedManualNorm != null && j < syncedManualNorm.Length && syncedManualNorm[j] == exactNorm)
                {
                    isDup = true;
                    break;
                }
            }
            
            if (!isDup)
            {
                syncedManual[write] = exact;
                if (syncedManualNorm != null && write < syncedManualNorm.Length)
                {
                    syncedManualNorm[write] = exactNorm;
                }
                write++;
            }
        }
        
        // Clear remaining entries
        for (int i = write; i < syncedManualLen; i++)
        {
            syncedManual[i] = null;
            if (syncedManualNorm != null && i < syncedManualNorm.Length)
            {
                syncedManualNorm[i] = null;
            }
        }
        
        syncedManualCount = write;
        ClearAccessCache();
    }

    private void CompactSyncedDj()
    {
        if (syncedDj == null) return;
        
        int write = 0;
        int syncedDjLen = syncedDj.Length;
        int maxIndex = Mathf.Min(syncedDjCount, syncedDjLen);
        
        for (int i = 0; i < maxIndex; i++)
        {
            string s = syncedDj[i];
            if (string.IsNullOrEmpty(s)) continue;
            
            string exact = NormalizeName(s);
            if (string.IsNullOrEmpty(exact)) continue;
            
            // Case-insensitive dedup matching the CompactSyncedManual fix.
            string exactNorm = NormalizeForCompare(exact);
            bool isDup = false;
            for (int j = 0; j < write; j++)
            {
                if (syncedDjNorm != null && j < syncedDjNorm.Length && syncedDjNorm[j] == exactNorm)
                {
                    isDup = true;
                    break;
                }
            }
            
            if (!isDup)
            {
                syncedDj[write] = exact;
                if (syncedDjNorm != null && write < syncedDjNorm.Length)
                {
                    syncedDjNorm[write] = exactNorm;
                }
                write++;
            }
        }
        
        // Clear remaining entries
        for (int i = write; i < syncedDjLen; i++)
        {
            syncedDj[i] = null;
        }
        if (syncedDjNorm != null)
        {
            int normLen = syncedDjNorm.Length;
            for (int i = write; i < normLen; i++)
            {
                syncedDjNorm[i] = null;
            }
        }
        
        syncedDjCount = write;
        ClearAccessCache();
    }

    // Optimized array indexOf with normalized array support
    private int ArrayIndexOf(string[] arr, int count, string value)
    {
        if (arr == null || count <= 0 || string.IsNullOrEmpty(value)) return -1;
        
        string target = NormalizeForCompare(value);
        if (string.IsNullOrEmpty(target)) return -1;
        
        // Use normalized parallel arrays if available for faster comparisons
        string[] normArr = null;
        if (arr == syncedManual && syncedManualNorm != null) normArr = syncedManualNorm;
        else if (arr == syncedDj && syncedDjNorm != null) normArr = syncedDjNorm;

        int arrLen = arr.Length;
        int maxIndex = Mathf.Min(count, arrLen);

        if (normArr != null)
        {
            int normLen = normArr.Length;
            for (int i = 0; i < maxIndex; i++)
            {
                if (i >= normLen) break;
                var nv = normArr[i];
                if (string.IsNullOrEmpty(nv)) continue;
                if (nv == target) return i;
            }
        }
        else
        {
            for (int i = 0; i < maxIndex; i++)
            {
                var v = arr[i];
                if (string.IsNullOrEmpty(v)) continue;
                if (NormalizeForCompare(v) == target) return i;
            }
        }
        return -1;
    }

    // Optimized array contains check with normalized array support
    private bool ArrayContains(string[] arr, int count, string value)
    {
        if (arr == null || count <= 0 || string.IsNullOrEmpty(value)) return false;
        
        string target = NormalizeForCompare(value);
        if (string.IsNullOrEmpty(target)) return false;
        
        // Use normalized parallel array if available for faster comparisons
        string[] normArr = null;
        if (arr == syncedManual && syncedManualNorm != null) normArr = syncedManualNorm;
        else if (arr == syncedDj && syncedDjNorm != null) normArr = syncedDjNorm;

        int arrLen = arr.Length;
        int maxIndex = Mathf.Min(count, arrLen);
        
        if (normArr != null)
        {
            int normLen = normArr.Length;
            for (int i = 0; i < maxIndex; i++)
            {
                if (i >= normLen) break;
                var nv = normArr[i];
                if (string.IsNullOrEmpty(nv)) continue;
                if (nv == target) return true;
            }
        }
        else
        {
            for (int i = 0; i < maxIndex; i++)
            {
                var v = arr[i];
                if (string.IsNullOrEmpty(v)) continue;
                if (NormalizeForCompare(v) == target) return true;
            }
        }
        return false;
    }

    // helper for inspector whitelists
    private bool IsInInspectorList(string[] arr, string name)
    {
        if (arr == null || string.IsNullOrEmpty(name)) return false;
        string key = NormalizeName(name);
        for (int i = 0; i < arr.Length; i++)
        {
            if (string.IsNullOrEmpty(arr[i])) continue;
            if (NormalizeName(arr[i]) == key) return true;
        }
        return false;
    }

    // Safe accessor for per-role member counts to avoid out-of-range indexing
    private int GetRoleMemberCountSafe(int idx)
    {
        if (roleMemberCount == null) return 0;
        if (idx < 0 || idx >= roleMemberCount.Length) return 0;
        return roleMemberCount[idx];
    }

    // Return total number of roles configured in inspector
    private int GetRoleCount()
    {
        return roleNames != null ? roleNames.Length : 0;
    }

    // Public accessor used by editor to inspect parsed member counts
    public int GetRoleMemberCountPublic(int idx)
    {
        return GetRoleMemberCountSafe(idx);
    }

    // Return the current length of the synced manual array
    private int GetSyncedManualArrayLength()
    {
        return syncedManual != null ? syncedManual.Length : 0;
    }

    // Determine role index for a given player name. Honors inspector array order (earlier = higher priority).
    public int GetRoleIndex(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;
        
        EnsureRoleBuffersOnce();
        
        // Use NormalizeForCompare (trim + lowercase) as the cache key so "Alice" and "alice"
        // always map to the same cache slot. Previously used NormalizeName (trim-only, case-preserving)
        // which caused duplicate cache entries for the same name with different casings.
        string exact = NormalizeForCompare(StripRolePrefix(name));
        if (string.IsNullOrEmpty(exact)) return -1;

        // Check cache first (only if cache is initialized)
        if (_roleIndexCacheKeys != null && _roleIndexCacheVals != null)
        {
            int cacheSize = _roleIndexCacheCount;
            for (int i = 0; i < cacheSize; i++)
            {
                if (_roleIndexCacheKeys[i] == exact)
                {
                    return _roleIndexCacheVals[i];
                }
            }
        }

        // Cache miss - search roles
        int rcount = _cachedRoleCount;
        for (int i = 0; i < rcount; i++)
        {
            int cnt = GetRoleMemberCountPublic(i);
            if (RoleArrayContains(i, cnt, exact))
            {
                // Store positive result in cache (only if cache is initialized)
                if (_roleIndexCacheKeys != null && _roleIndexCacheVals != null)
                {
                    int cacheLen = _roleIndexCacheKeys.Length;
                    int storePos;
                    if (_roleIndexCacheCount < cacheLen)
                    {
                        // Cache not full yet - append new entry
                        storePos = _roleIndexCacheCount++;
                    }
                    else
                    {
                        // Cache full - evict oldest entry using circular buffer
                        storePos = _roleIndexCacheNextEvict;
                        _roleIndexCacheNextEvict = (_roleIndexCacheNextEvict + 1) % cacheLen;
                        if (enableDebugLogs)
                        {
                            string evictedKey = _roleIndexCacheKeys[storePos];
                            if (!string.IsNullOrEmpty(evictedKey))
                            {
                                DebugLog("Role index cache full (" + cacheLen.ToString() + " entries) - evicting oldest entry: '" + evictedKey + "'");
                            }
                        }
                    }
                    _roleIndexCacheKeys[storePos] = exact;
                    _roleIndexCacheVals[storePos] = i;
                }
                return i;
            }
        }

        // Store negative result in cache (only if cache is initialized)
        if (_roleIndexCacheKeys != null && _roleIndexCacheVals != null)
        {
            int cacheLen2 = _roleIndexCacheKeys.Length;
            int store;
            if (_roleIndexCacheCount < cacheLen2)
            {
                // Cache not full yet - append new entry
                store = _roleIndexCacheCount++;
            }
            else
            {
                // Cache full - evict oldest entry using circular buffer
                store = _roleIndexCacheNextEvict;
                _roleIndexCacheNextEvict = (_roleIndexCacheNextEvict + 1) % cacheLen2;
                if (enableDebugLogs)
                {
                    string evictedKey = _roleIndexCacheKeys[store];
                    if (!string.IsNullOrEmpty(evictedKey))
                    {
                        DebugLog("Role index cache full (" + cacheLen2.ToString() + " entries) - evicting oldest entry: '" + evictedKey + "'");
                    }
                }
            }
            _roleIndexCacheKeys[store] = exact;
            _roleIndexCacheVals[store] = -1;
        }
        return -1;
    }

    // Check if a name is a configured super admin
    public bool IsSuperAdmin(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        
        // Use NormalizeForCompare (trim + lowercase) so cache keys are always lowercase,
        // preventing duplicate cache entries for the same name with different casing.
        string keyNorm = NormalizeForCompare(StripRolePrefix(name));
        if (string.IsNullOrEmpty(keyNorm)) return false;
        
        // Check cache first (only if cache is initialized)
        if (_isSuperAdminCacheKeys != null && _isSuperAdminCacheVals != null)
        {
            int cacheSize = _isSuperAdminCacheCount;
            for (int i = 0; i < cacheSize; i++)
            {
                if (_isSuperAdminCacheKeys[i] == keyNorm)
                {
                    return _isSuperAdminCacheVals[i];
                }
            }
        }
        
        // Check for explicit role prefix like "(Super Admin) Alice"
        string trimmed = name.Trim();
        if (trimmed.Length > 0 && trimmed[0] == '(')
        {
            int idx = trimmed.IndexOf(')');
            if (idx > 0)
            {
                string prefix = trimmed.Substring(1, idx - 1).Trim();
                if (!string.IsNullOrEmpty(prefix))
                {
                    string prefixLower = prefix.ToLowerInvariant();
                    if (prefixLower.Contains("super") && prefixLower.Contains("admin"))
                    {
                        // Store in cache before returning (only if cache is initialized)
                        if (_isSuperAdminCacheKeys != null && _isSuperAdminCacheVals != null)
                        {
                            int cacheLen = _isSuperAdminCacheKeys.Length;
                            int store;
                            if (_isSuperAdminCacheCount < cacheLen)
                            {
                                // Cache not full yet - append new entry
                                store = _isSuperAdminCacheCount++;
                            }
                            else
                            {
                                // Cache full - evict oldest entry using circular buffer
                                store = _isSuperAdminCacheNextEvict;
                                _isSuperAdminCacheNextEvict = (_isSuperAdminCacheNextEvict + 1) % cacheLen;
                                if (enableDebugLogs)
                                {
                                    string evictedKey = _isSuperAdminCacheKeys[store];
                                    if (!string.IsNullOrEmpty(evictedKey))
                                    {
                                        DebugLog("IsSuperAdmin cache full (" + cacheLen.ToString() + " entries) - evicting oldest entry: '" + evictedKey + "'");
                                    }
                                }
                            }
                            _isSuperAdminCacheKeys[store] = keyNorm;
                            _isSuperAdminCacheVals[store] = true;
                        }
                        return true;
                    }
                }
            }
        }

        EnsureRoleBuffersOnce();

        // keyNorm is already lowercase from NormalizeForCompare above; no need to call ToLowerInvariant() again.

        // Check explicit whitelist entries (pre-normalized)
        if (_superAdminWhitelistNormalized != null)
        {
            int whitelistLen = _superAdminWhitelistNormalized.Length;
            for (int i = 0; i < whitelistLen; i++)
            {
                string w = _superAdminWhitelistNormalized[i];
                if (string.IsNullOrEmpty(w)) continue;
                if (w == keyNorm)
                {
                    // Store in cache before returning (only if cache is initialized)
                    if (_isSuperAdminCacheKeys != null && _isSuperAdminCacheVals != null)
                    {
                        int cacheLen = _isSuperAdminCacheKeys.Length;
                        int store;
                        if (_isSuperAdminCacheCount < cacheLen)
                        {
                            // Cache not full yet - append new entry
                            store = _isSuperAdminCacheCount++;
                        }
                        else
                        {
                            // Cache full - evict oldest entry using circular buffer
                            store = _isSuperAdminCacheNextEvict;
                            _isSuperAdminCacheNextEvict = (_isSuperAdminCacheNextEvict + 1) % cacheLen;
                            if (enableDebugLogs)
                            {
                                string evictedKey = _isSuperAdminCacheKeys[store];
                                if (!string.IsNullOrEmpty(evictedKey))
                                {
                                    DebugLog("IsSuperAdmin cache full (" + cacheLen.ToString() + " entries) - evicting oldest entry: '" + evictedKey + "'");
                                }
                            }
                        }
                        _isSuperAdminCacheKeys[store] = keyNorm;
                        _isSuperAdminCacheVals[store] = true;
                    }
                    return true;
                }
            }
        }

        // Check members of "Super Admin" role
        bool result = false;
        if (_superRoleIndex >= 0)
        {
            int cnt = GetRoleMemberCountSafe(_superRoleIndex);
            if (cnt > 0 && RoleArrayContains(_superRoleIndex, cnt, keyNorm))
            {
                result = true;
            }
        }

        // Store result in cache using circular buffer with proper FIFO eviction (only if cache is initialized)
        if (_isSuperAdminCacheKeys != null && _isSuperAdminCacheVals != null)
        {
            int cacheLen3 = _isSuperAdminCacheKeys.Length;
            int storeIdx;
            if (_isSuperAdminCacheCount < cacheLen3)
            {
                // Cache not full yet - append new entry
                storeIdx = _isSuperAdminCacheCount++;
            }
            else
            {
                // Cache full - evict oldest entry using circular buffer
                storeIdx = _isSuperAdminCacheNextEvict;
                _isSuperAdminCacheNextEvict = (_isSuperAdminCacheNextEvict + 1) % cacheLen3;
                if (enableDebugLogs)
                {
                    string evictedKey = _isSuperAdminCacheKeys[storeIdx];
                    if (!string.IsNullOrEmpty(evictedKey))
                    {
                        DebugLog("IsSuperAdmin cache full (" + cacheLen3.ToString() + " entries) - evicting oldest entry: '" + evictedKey + "'");
                    }
                }
            }
            _isSuperAdminCacheKeys[storeIdx] = keyNorm;
            _isSuperAdminCacheVals[storeIdx] = result;
        }
        
        return result;
    }

    // Public manual list accessors used by UI
    public int GetManualCount()
    {
        return syncedManualCount;
    }

    public string GetManualAt(int idx)
    {
        if (syncedManual == null) return null;
        if (idx < 0 || idx >= syncedManualCount) return null;
        return syncedManual[idx];
    }

    // Lightweight notify that updates only the changed player's row in registered UIs.
    public void NotifyListsForName(string playerName, bool isAuthed)
    {
        if (string.IsNullOrEmpty(playerName)) return;
        if (lists != null)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                var ui = lists[i];
                if (ui == null) continue;
                ui.UpdateRowForName(playerName, isAuthed);
            }
        }
    }

    // Add/remove manual entries (synchronized). These are simple and serialize when owner; otherwise defer until ownership obtained.
    private void ManualAdd(string exact)
    {
        if (string.IsNullOrEmpty(exact)) return;
        
        EnsureSyncedArrayCapacity();
        
        // Avoid duplicates using case-insensitive comparison (via syncedManualNorm) so that
        // "Alice" and "alice" are not both stored. Previously used case-sensitive == which
        // could allow case-variant duplicates to slip through.
        if (ArrayContains(syncedManual, syncedManualCount, exact)) return;
        
        int manualLen = syncedManual.Length;
        if (syncedManualCount >= manualLen) return;
        
        syncedManual[syncedManualCount] = exact;
        if (syncedManualNorm != null && syncedManualCount < syncedManualNorm.Length)
        {
            syncedManualNorm[syncedManualCount] = NormalizeForCompare(exact);
        }
        syncedManualCount++;
        
        // Mark dirty so we can serialize when/if we become owner
        manualDirty = true;

        if (Networking.IsOwner(gameObject))
        {
            TrySerializeManualChanges();
        }
        
        // Local runtime cache invalidated because manual list changed
        ClearAccessCache();
    }

    // Manual DJ list management
    public void DjAdd(string exact)
    {
        if (string.IsNullOrEmpty(exact)) return;
        
        // Initialize if needed
        if (syncedDj == null || syncedDj.Length == 0)
        {
            int cap = GetEffectiveMaxSyncedManual();
            syncedDj = new string[cap];
            syncedDjNorm = new string[cap];
            syncedDjCount = 0;
        }
        
        // Avoid duplicates using case-insensitive comparison (matching ManualAdd fix).
        if (ArrayContains(syncedDj, syncedDjCount, exact)) return;
        
        int djLen = syncedDj.Length;
        if (syncedDjCount >= djLen) return;
        
        syncedDj[syncedDjCount] = exact;
        if (syncedDjNorm != null && syncedDjCount < syncedDjNorm.Length)
        {
            syncedDjNorm[syncedDjCount] = NormalizeForCompare(exact);
        }
        syncedDjCount++;
        
        manualDirty = true;
        
        if (Networking.IsOwner(gameObject))
        {
            TrySerializeManualChanges();
        }
        
        ClearAccessCache();
    }

    public void DjRemove(string exact)
    {
        if (string.IsNullOrEmpty(exact) || syncedDj == null) return;
        
        // Find the index to remove using case-insensitive lookup (matching ManualRemove fix).
        int idx = ArrayIndexOf(syncedDj, syncedDjCount, exact);
        
        if (idx == -1) return;
        
        // Shift remaining entries down
        for (int i = idx + 1; i < syncedDjCount; i++)
        {
            syncedDj[i - 1] = syncedDj[i];
            if (syncedDjNorm != null && i < syncedDjNorm.Length)
            {
                syncedDjNorm[i - 1] = syncedDjNorm[i];
            }
        }
        
        syncedDj[syncedDjCount - 1] = null;
        if (syncedDjNorm != null && syncedDjCount - 1 < syncedDjNorm.Length)
        {
            syncedDjNorm[syncedDjCount - 1] = null;
        }
        syncedDjCount--;
        
        manualDirty = true;
        
        if (Networking.IsOwner(gameObject))
        {
            TrySerializeManualChanges();
        }
        
        ClearAccessCache();
    }

    // Evaluate DJ access for local player and toggle configured DJ area objects
    public void EvaluateLocalDjAccess()
    {
        EvaluateLocalDjAccess(false);
    }

    // Overload with forceUpdate to force toggles/logging even if state didn't change
    public void EvaluateLocalDjAccess(bool forceUpdate)
    {
        // avoid repeated work if called multiple times in the same frame
        int fc = Time.frameCount;
        if (!forceUpdate && _lastDjEvalFrame == fc) return;
        _lastDjEvalFrame = fc;

        VRCPlayerApi lp = GetLocalPlayer();
        bool localDj = false;
        string localName = "(unknown)";
        if (Utilities.IsValid(lp))
        {
            // Use cached normalized name to avoid repeated displayName access and normalization
            string normalized = NormalizeNameFromPlayer(lp);
            if (!string.IsNullOrEmpty(normalized))
            {
                // Treat Super Admins as always having DJ access so configured DJ area objects
                // are updated appropriately for them even if they're not in the DJ list.
                localDj = IsDj(normalized) || IsSuperAdmin(lp);
                // For display/logging, use the original cached displayName
                localName = GetCachedDisplayName(lp);
            }
        }

        bool changed = (localDj != lastLocalDj);

        if (djAreaObjects != null && (forceUpdate || changed))
        {
            for (int i = 0; i < djAreaObjects.Length; i++)
            {
                var go = djAreaObjects[i];
                if (go == null) continue;
                // Objects are configured to be disabled when the local player is a DJ,
                // so set active state to the inverse of localDj. Only change if different to avoid churn.
                bool desired = !localDj;
                if (go.activeSelf != desired) go.SetActive(desired);
            }
        }

        // Debug only when forced or state changed and respect a small cooldown to avoid spam
        if (forceUpdate || changed)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastDjLogTime >= 1.0f) // at most once per second
            {
                if (enableDebugLogs)
                {
                    DebugLog("EvaluateLocalDjAccess: player '" + localName + "' dj: " + (localDj ? "YES" : "NO"));
                }
                _lastDjLogTime = now;
            }
        }

        lastLocalDj = localDj;
    }

    // Optimized manual remove with proper array shifting
    private void ManualRemove(string exact)
    {
        if (string.IsNullOrEmpty(exact) || syncedManual == null) return;
        
        // Find the index to remove using case-insensitive lookup (via syncedManualNorm).
        // Previously used case-sensitive == which silently failed when stored casing differed.
        int idx = ArrayIndexOf(syncedManual, syncedManualCount, exact);
        
        if (idx == -1) return;
        
        // Shift remaining entries down in both arrays
        for (int i = idx + 1; i < syncedManualCount; i++)
        {
            syncedManual[i - 1] = syncedManual[i];
            if (syncedManualNorm != null && i < syncedManualNorm.Length)
            {
                syncedManualNorm[i - 1] = syncedManualNorm[i];
            }
        }
        
        // Clear the last entry
        syncedManual[syncedManualCount - 1] = null;
        if (syncedManualNorm != null && syncedManualCount - 1 < syncedManualNorm.Length)
        {
            syncedManualNorm[syncedManualCount - 1] = null;
        }
        
        syncedManualCount--;
        
        // Mark dirty so we can serialize when/if we become owner
        manualDirty = true;

        if (Networking.IsOwner(gameObject))
        {
            TrySerializeManualChanges();
        }
        
        // Local runtime cache invalidated because manual list changed
        ClearAccessCache();
    }

    // Determine if a name is authed (manual, role-based with vip permission, or superadmin)
    public bool IsAuthed(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        EnsureRoleBuffersOnce();
        // Use NormalizeForCompare (trim + lowercase) so "Alice" and "alice" share the same cache
        // slot. Previously used NormalizeName (trim-only) which caused duplicate access-cache entries
        // for the same name with different casings, wasting cache capacity.
        string exact = NormalizeForCompare(StripRolePrefix(name));
        if (string.IsNullOrEmpty(exact)) return false;
        int idx = EnsureAccessCached(exact);
        if (idx >= 0) return _accessCacheAuthed[idx];
        return false;
    }

    // Determine if a name has DJ access (manual DJ list or role-based DJ permission)
    public bool IsDj(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        EnsureRoleBuffersOnce();
        // Use NormalizeForCompare (trim + lowercase) for the same cache-key consistency reason as IsAuthed.
        string exact = NormalizeForCompare(StripRolePrefix(name));
        if (string.IsNullOrEmpty(exact)) return false;
        int idx = EnsureAccessCached(exact);
        if (idx >= 0) return _accessCacheDj[idx];
        return false;
    }

    // Refreshes the per-frame cache of local player edit permissions.
    // Converts CanLocalEditTarget from O(rcount) per row to O(rcount) once per frame.
    private void RefreshLocalEditPermCache()
    {
        int fc = Time.frameCount;
        if (_cachedLocalEditPermFrame == fc) return;
        _cachedLocalEditPermFrame = fc;
        VRCPlayerApi lp = GetLocalPlayer();
        if (!Utilities.IsValid(lp))
        {
            _cachedLocalIsSuperAdminVal = false;
            _cachedLocalHasEditRole = false;
            return;
        }
        if (IsSuperAdmin(lp))
        {
            _cachedLocalIsSuperAdminVal = true;
            _cachedLocalHasEditRole = true;
            return;
        }
        _cachedLocalIsSuperAdminVal = false;
        string local = NormalizeNameFromPlayer(lp);
        if (string.IsNullOrEmpty(local)) { _cachedLocalHasEditRole = false; return; }
        EnsureRoleBuffersOnce();
        int rcount = _cachedRoleCount;
        for (int i = 0; i < rcount; i++)
        {
            bool canAdd = roleCanAddPlayers != null && i < roleCanAddPlayers.Length && roleCanAddPlayers[i];
            bool canRevoke = roleCanRevokePlayers != null && i < roleCanRevokePlayers.Length && roleCanRevokePlayers[i];
            if (!canAdd && !canRevoke) continue;
            int cnt = GetRoleMemberCountPublic(i);
            if (RoleArrayContains(i, cnt, local)) { _cachedLocalHasEditRole = true; return; }
        }
        _cachedLocalHasEditRole = false;
    }

    public bool CanLocalEditTarget(string targetName)
    {
        VRCPlayerApi lp = GetLocalPlayer();
        if (!Utilities.IsValid(lp)) return false;
        // cannot edit read-only targets (applies to everyone, including super admins)
        if (IsTargetReadOnly(targetName)) return false;
        // Use per-frame cached permissions: O(rcount) once per frame instead of O(rcount) per row.
        RefreshLocalEditPermCache();
        return _cachedLocalHasEditRole;
    }

    public bool CanLocalEditAny()
    {
        VRCPlayerApi lp = GetLocalPlayer();
        if (!Utilities.IsValid(lp)) return false;
        string local = NormalizeNameFromPlayer(lp);
        return CanLocalEditTarget(local);
    }

    // Determine if local player can manage DJ roles (superadmins or any role with VIP access)
    public bool CanLocalManageDj()
    {
        VRCPlayerApi lp = GetLocalPlayer();
        if (!Utilities.IsValid(lp)) return false;
        if (IsSuperAdmin(lp)) return true;
        string local = NormalizeNameFromPlayer(lp);
        // Allow the initial owner who started the instance to manage DJs
        if (!string.IsNullOrEmpty(initialOwner) && !string.IsNullOrEmpty(local) && initialOwner == local) return true;
        if (string.IsNullOrEmpty(local)) return false;
        EnsureRoleBuffersOnce();
        int rcount = _cachedRoleCount;
        for (int i = 0; i < rcount; i++)
        {
            bool vipPerm = roleCanVipAccess != null && i < roleCanVipAccess.Length ? roleCanVipAccess[i] : false;
            if (!vipPerm) continue;
            int cnt = GetRoleMemberCountPublic(i);
            if (RoleArrayContains(i, cnt, local)) return true;
        }
        return false;
    }

    private void ApplyDjSystemStateToList(VipWhitelistUI ui)
    {
        if (ui == null) return;
        ui.SetDjSystemEnabled(syncedDjSystemEnabled);
    }

    private void BroadcastDjSystemState()
    {
        if (lists != null)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                var ui = lists[i];
                if (ui == null) continue;
                ApplyDjSystemStateToList(ui);
            }
        }
    }

    public void SetDjSystemEnabledState(bool enabled)
    {
        if (syncedDjSystemEnabled == enabled) return;
        syncedDjSystemEnabled = enabled;
        djSystemDirty = true;
        BroadcastDjSystemState();
        if (Networking.IsOwner(gameObject))
        {
            TrySerializeManualChanges();
        }
        else
        {
            VRCPlayerApi lp = GetLocalPlayer();
            if (Utilities.IsValid(lp))
            {
                Networking.SetOwner(lp, gameObject);
            }
        }
    }

    // track last notified versions to avoid expensive full rebuilds when not needed
    private int _lastNotifiedRoleListVersion = -1;
    private int _lastNotifiedManualCount = -1;

    // Notify registered lists to rebuild/refresh
    public void NotifyLists()
    {
        bool needRebuild = false;
        if (roleListVersion != _lastNotifiedRoleListVersion) needRebuild = true;
        if (syncedManualCount != _lastNotifiedManualCount) needRebuild = true;

        // Notify inspector-assigned lists
        if (lists != null)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                var ui = lists[i];
                if (ui == null) continue;
                if (needRebuild)
                {
                    ui.RebuildPlayerList();
                }
                ui.UpdateRowTogglesFromAuth();
            }
        }

        if (needRebuild)
        {
            _lastNotifiedRoleListVersion = roleListVersion;
            _lastNotifiedManualCount = syncedManualCount;
        }
    }

    // Evaluate local player's access, update local-only objects and notify UIs to refresh toggles
    public void EvaluateLocalAccess(bool forceUpdate = false)
    {
        VRCPlayerApi lp = GetLocalPlayer();
        bool localAuthed = false;
        string localName = "(unknown)";
        if (Utilities.IsValid(lp))
        {
            // Use cached normalized name to avoid repeated displayName access
            string normalized = NormalizeNameFromPlayer(lp);
            if (!string.IsNullOrEmpty(normalized))
            {
                localAuthed = IsAuthed(normalized);
                // For display/logging, use the original cached displayName
                localName = GetCachedDisplayName(lp);
            }
        }

        // Disable or enable configured objects locally when local player is authed
        if (objectsToDisableWhenAuthed != null)
        {
            for (int i = 0; i < objectsToDisableWhenAuthed.Length; i++)
            {
                var go = objectsToDisableWhenAuthed[i];
                if (go == null) continue;
                go.SetActive(!localAuthed);
            }
        }

        bool changed = (localAuthed != lastLocalAuthed);

        // Debug: log VIP access state and object statuses (only when changed or forced)
        if (enableDebugLogs && (forceUpdate || changed))
        {
            string summary = "EvaluateLocalAccess: player '" + localName + "' authed: " + (localAuthed ? "YES" : "NO") + ". VIP access: " + (localAuthed ? "ENABLED" : "DISABLED") + ". Objects:";
            if (objectsToDisableWhenAuthed != null)
            {
                for (int i = 0; i < objectsToDisableWhenAuthed.Length; i++)
                {
                    var go = objectsToDisableWhenAuthed[i];
                    if (go == null) continue;
                    summary += " [" + go.name + ": " + (go.activeSelf ? "active" : "inactive") + "]";
                }
            }
            Debug.Log("<color=#00ffff>(VIP Manager) " + summary + "</color>");
        }

        // Only notify UIs if forced or auth state changed
        if (forceUpdate || changed)
        {
            // Lightweight: update each registered UI to refresh row toggles/interactivity
            if (lists != null)
            {
                for (int i = 0; i < lists.Length; i++)
                {
                    var ui = lists[i];
                    if (ui == null) continue;
                    ui.UpdateRowTogglesFromAuth();
                }
            }
        }

        lastLocalAuthed = localAuthed;
    }

    // Helper: find index of role whose roleName matches (case-insensitive contains)
    public int GetRoleIndexByRoleName(string roleNameQuery)
    {
        if (string.IsNullOrEmpty(roleNameQuery) || roleNames == null) return -1;
        string q = NormalizeForCompare(roleNameQuery);
        for (int i = 0; i < roleNames.Length; i++)
        {
            if (string.IsNullOrEmpty(roleNames[i])) continue;
            string rn = NormalizeForCompare(roleNames[i]);
            if (rn == q || rn.Contains(q)) return i;
        }
        return -1;
    }

    // Public accessors used by VipWhitelistUI
    public string GetRoleNameByIndex(int idx)
    {
        if (roleNames == null || idx < 0 || idx >= roleNames.Length) return null;
        return roleNames[idx];
    }

    public Color GetRoleColorByIndex(int idx)
    {
        if (roleColors == null || idx < 0 || idx >= roleColors.Length) return Color.white;
        return roleColors[idx];
    }

    public bool GetRoleCanAdd(int idx)
    {
        if (roleCanAddPlayers == null || idx < 0 || idx >= roleCanAddPlayers.Length) return false;
        return roleCanAddPlayers[idx];
    }

    public bool GetRoleCanRevoke(int idx)
    {
        if (roleCanRevokePlayers == null || idx < 0 || idx >= roleCanRevokePlayers.Length) return false;
        return roleCanRevokePlayers[idx];
    }

    public bool GetSyncedDjSystemEnabled()
    {
        return syncedDjSystemEnabled;
    }

    public bool GetRoleCanDj(int idx)
    {
        if (roleCanDjAccess == null || idx < 0 || idx >= roleCanDjAccess.Length) return false;
        return roleCanDjAccess[idx];
    }

    // Public accessor to get a specific member of a role
    public string GetRoleMemberAt(int roleIdx, int memberIdx)
    {
        if (roleIdx < 0) return null;
        if (roleMemberCount == null) return null;
        if (roleIdx >= roleMemberCount.Length) return null;
        int cnt = roleMemberCount[roleIdx];
        if (memberIdx < 0 || memberIdx >= cnt) return null;
        int offset = RoleMemberOffset(roleIdx);
        int idx = offset + memberIdx;
        if (roleMembersFlat == null || idx < 0 || idx >= roleMembersFlat.Length) return null;
        return roleMembersFlat[idx];
    }

    private void ScheduleManualSerializationRetry(int delayFrames = MIN_SERIALIZATION_FRAME_INTERVAL)
    {
        if (_serializationRetryScheduled) return;
        if (!Networking.IsOwner(gameObject)) return;
        _serializationRetryScheduled = true;
        int frames = Mathf.Max(1, delayFrames);
        SendCustomEventDelayedFrames(nameof(ProcessPendingManualSerialization), frames);
    }

    public void ProcessPendingManualSerialization()
    {
        _serializationRetryScheduled = false;
        TrySerializeManualChanges();
    }

    private void TrySerializeManualChanges()
    {
        if (!Networking.IsOwner(gameObject) || (!manualDirty && !djSystemDirty)) return;
        if (Networking.IsClogged)
        {
            if (enableDebugLogs) Debug.Log("(VIP Manager) RequestSerialization deferred due to network clog");
            ScheduleManualSerializationRetry();
            return;
        }
        int currentFrame = Time.frameCount;
        int framesSinceLast = currentFrame - _lastSerializationFrame;
        if (framesSinceLast < MIN_SERIALIZATION_FRAME_INTERVAL)
        {
            if (enableDebugLogs) Debug.Log("(VIP Manager) Manual serialization deferred to maintain >=10Hz");
            ScheduleManualSerializationRetry(MIN_SERIALIZATION_FRAME_INTERVAL - framesSinceLast);
            return;
        }

        RequestSerialization();
        _lastSerializationFrame = currentFrame;
        manualDirty = false;
        djSystemDirty = false;
    }

    // Called by UI when a row toggle is changed. Adds/removes manual entries and notifies UIs.
    public void OnRowToggled(string playerName, bool isOn)
    {
        if (string.IsNullOrEmpty(playerName)) return;
        string exact = NormalizeName(playerName);

        // Avoid duplicate handling: if the manual list already reflects the desired state,
        // ignore the event. This prevents multiple identical events (e.g. from multiple
        // UI instances or redundant forwarding) from causing repeated logs/serializations.
        bool currentlyInManual = ArrayContains(syncedManual, syncedManualCount, exact);
        if (isOn && currentlyInManual) return;   // already added
        if (!isOn && !currentlyInManual) return; // already removed

        if (enableDebugLogs)
        {
            DebugLog("OnRowToggled: '" + exact + "' isOn=" + (isOn ? "true" : "false"));
        }

        // Ensure local player is owner before attempting to serialize. Avoid unnecessary SetOwner if already owner.
        VRCPlayerApi lp = GetLocalPlayer();
        if (Utilities.IsValid(lp) && !Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(lp, gameObject);
            // Do not RequestSerialization here because ownership transfer is asynchronous.
            // ManualAdd/Remove will mark manualDirty and OnOwnershipTransferred will flush serialization.
        }

        if (isOn) ManualAdd(exact);
        else ManualRemove(exact);

        // lightweight DJ manual add/remove if toggle event indicates DJ changed via UI (UI will call DJToggled separately)
        // Note: DJ toggles are handled by UI via DJToggled custom event; this code remains a hook point.

        // Lightweight notify and update local access state
        NotifyListsForName(exact, isOn);
        EvaluateLocalAccess();
        // Also update DJ areas for local player in case their DJ status changed
        EvaluateLocalDjAccess();
    }

    // temp buffer for player lookups when checking in-world presence
    private VRCPlayerApi[] _playerBufForNotify = new VRCPlayerApi[64];

    // When we gain ownership, flush any pending manual changes by serializing once.
    public override void OnOwnershipTransferred(VRCPlayerApi newOwner)
    {
        // If the initial owner hasn't been recorded yet, record the first owner who gains
        // ownership of the object. This ensures the instance remembers its original owner
        // so they can be granted management permission when they return.
        if (string.IsNullOrEmpty(initialOwner) && Utilities.IsValid(newOwner))
        {
            string ownerName = NormalizeNameFromPlayer(newOwner);
            if (!string.IsNullOrEmpty(ownerName))
            {
                // Only the current local owner should write and serialize the synced value.
                if (Networking.IsOwner(gameObject))
                {
                    initialOwner = ownerName;
                    if (enableDebugLogs)
                    {
                        DebugLog("OnOwnershipTransferred: recorded initial owner '" + initialOwner + "'");
                    }
                    if (!Networking.IsClogged)
                    {
                        RequestSerialization();
                    }
                    else
                    {
                        DebugLog("OnOwnershipTransferred: deferring initialOwner serialization because network is clogged");
                    }
                }
            }
        }

        // Only the new owner should serialize pending manual/DJ system changes
        if (Networking.IsOwner(gameObject) && (manualDirty || djSystemDirty))
        {
            DebugLog("OnOwnershipTransferred: local became owner, flushing manual/DJ system changes");
            TrySerializeManualChanges();
        }
    }
}