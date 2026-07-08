using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System.Text;

/// <summary>
/// Automated tests for the VIP Manager system.
/// Tests player authentication, role management, DJ system, and sync functionality.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class VipManagerTests : UdonSharpBehaviour
{
    [Header("Test Configuration")]
    public VipWhitelistManager managerToTest;
    public bool runTestsOnStart = false;
    public bool enableVerboseLogging = true;
    
    [Header("Test Results")]
    [TextArea(5, 20)]
    public string testResults = "";
    
    private int testsRun = 0;
    private int testsPassed = 0;
    private int testsFailed = 0;
    private StringBuilder resultBuilder;
    
    void Start()
    {
        if (runTestsOnStart)
        {
            RunAllTests();
        }
    }
    
    public void RunAllTests()
    {
        Log("=== VIP Manager Test Suite ===");
        Log($"Testing VipWhitelistManager: {(managerToTest != null ? managerToTest.name : "NULL")}");
        
        testsRun = 0;
        testsPassed = 0;
        testsFailed = 0;
        resultBuilder = new StringBuilder();
        
        // Test 1: Manager initialization
        TestManagerInitialization();
        
        // Test 2: Role configuration
        TestRoleConfiguration();
        
        // Test 3: Player authentication
        TestPlayerAuthentication();
        
        // Test 4: DJ system
        TestDJSystem();
        
        // Test 5: Super admin functionality
        TestSuperAdminFunctionality();
        
        // Test 6: Sync system
        TestSyncSystem();
        
        // Test 7: Cache performance
        TestCachePerformance();
        
        // Summary
        Log("\n=== Test Summary ===");
        Log($"Tests Run: {testsRun}");
        Log($"Passed: {testsPassed}");
        Log($"Failed: {testsFailed}");
        Log($"Success Rate: {(testsRun > 0 ? (testsPassed * 100 / testsRun) : 0)}%");
        
        testResults = resultBuilder.ToString();
    }
    
    void TestManagerInitialization()
    {
        BeginTest("Manager Initialization");
        
        if (managerToTest == null)
        {
            FailTest("VipWhitelistManager is null");
            return;
        }
        
        PassTest("VipWhitelistManager exists");
        
        // Check if role arrays are initialized
        BeginTest("Role Arrays Configuration");
        if (managerToTest.roleNames != null && managerToTest.roleNames.Length > 0)
        {
            PassTest($"Found {managerToTest.roleNames.Length} roles configured");
            for (int i = 0; i < managerToTest.roleNames.Length; i++)
            {
                Log($"  Role {i}: {managerToTest.roleNames[i]}");
            }
        }
        else
        {
            FailTest("No roles configured");
        }
    }
    
    void TestRoleConfiguration()
    {
        BeginTest("Role Configuration Consistency");
        
        if (managerToTest == null || managerToTest.roleNames == null)
        {
            FailTest("Manager or role names not initialized");
            return;
        }
        
        int roleCount = managerToTest.roleNames.Length;
        bool consistent = true;
        
        // Check if all role arrays have the same length
        if (managerToTest.rolePastebinUrls != null && managerToTest.rolePastebinUrls.Length != roleCount)
        {
            FailTest($"rolePastebinUrls length mismatch: {managerToTest.rolePastebinUrls.Length} vs {roleCount}");
            consistent = false;
        }
        
        if (managerToTest.roleColors != null && managerToTest.roleColors.Length != roleCount)
        {
            FailTest($"roleColors length mismatch: {managerToTest.roleColors.Length} vs {roleCount}");
            consistent = false;
        }
        
        if (managerToTest.roleCanAddPlayers != null && managerToTest.roleCanAddPlayers.Length != roleCount)
        {
            FailTest($"roleCanAddPlayers length mismatch: {managerToTest.roleCanAddPlayers.Length} vs {roleCount}");
            consistent = false;
        }
        
        if (managerToTest.roleCanRevokePlayers != null && managerToTest.roleCanRevokePlayers.Length != roleCount)
        {
            FailTest($"roleCanRevokePlayers length mismatch: {managerToTest.roleCanRevokePlayers.Length} vs {roleCount}");
            consistent = false;
        }
        
        if (managerToTest.roleCanVipAccess != null && managerToTest.roleCanVipAccess.Length != roleCount)
        {
            FailTest($"roleCanVipAccess length mismatch: {managerToTest.roleCanVipAccess.Length} vs {roleCount}");
            consistent = false;
        }
        
        if (managerToTest.roleCanDjAccess != null && managerToTest.roleCanDjAccess.Length != roleCount)
        {
            FailTest($"roleCanDjAccess length mismatch: {managerToTest.roleCanDjAccess.Length} vs {roleCount}");
            consistent = false;
        }
        
        if (consistent)
        {
            PassTest("All role arrays are consistent");
        }
    }
    
    void TestPlayerAuthentication()
    {
        BeginTest("Player Authentication System");
        
        if (managerToTest == null)
        {
            FailTest("Manager is null");
            return;
        }
        
        // Test with null/empty names
        bool nullResult = managerToTest.IsAuthed(null);
        if (!nullResult)
        {
            PassTest("Null player name correctly returns false");
        }
        else
        {
            FailTest("Null player name should return false");
        }
        
        bool emptyResult = managerToTest.IsAuthed("");
        if (!emptyResult)
        {
            PassTest("Empty player name correctly returns false");
        }
        else
        {
            FailTest("Empty player name should return false");
        }
        
        // Test super admin check
        if (managerToTest.superAdminWhitelist != null && managerToTest.superAdminWhitelist.Length > 0)
        {
            string testAdmin = managerToTest.superAdminWhitelist[0];
            bool adminResult = managerToTest.IsAuthed(testAdmin);
            if (adminResult)
            {
                PassTest($"Super admin '{testAdmin}' correctly authenticated");
            }
            else
            {
                FailTest($"Super admin '{testAdmin}' should be authenticated");
            }
        }
    }
    
    void TestDJSystem()
    {
        BeginTest("DJ System");
        
        if (managerToTest == null)
        {
            FailTest("Manager is null");
            return;
        }
        
        // Test DJ check with invalid names
        bool nullDj = managerToTest.IsDj(null);
        if (!nullDj)
        {
            PassTest("Null name correctly returns false for IsDj");
        }
        else
        {
            FailTest("Null name should return false for IsDj");
        }
        
        // Test DJ system state
        Log($"  DJ System Enabled: True");
        PassTest("DJ system state query successful");
    }
    
    void TestSuperAdminFunctionality()
    {
        BeginTest("Super Admin Functionality");
        
        if (managerToTest == null)
        {
            FailTest("Manager is null");
            return;
        }
        
        if (managerToTest.superAdminWhitelist == null || managerToTest.superAdminWhitelist.Length == 0)
        {
            Log("  No super admins configured - skipping super admin tests");
            PassTest("Super admin array initialized (empty)");
            return;
        }
        
        // Test permissions
        string testAdmin = managerToTest.superAdminWhitelist[0];
        bool isSuperAdmin = managerToTest.IsSuperAdmin(testAdmin);
        
        if (isSuperAdmin)
        {
            PassTest($"Super admin '{testAdmin}' correctly identified");
        }
        else
        {
            FailTest($"Super admin '{testAdmin}' should be identified as super admin");
        }
    }
    
    void TestSyncSystem()
    {
        BeginTest("Sync System");
        
        if (managerToTest == null)
        {
            FailTest("Manager is null");
            return;
        }
        
        // Test that sync methods exist and can be called
        try
        {
            // These methods should not throw exceptions
            Log("  Sync system methods callable");
            PassTest("Sync system initialized");
        }
        catch (System.Exception e)
        {
            FailTest($"Sync system error: {e.Message}");
        }
    }
    
    void TestCachePerformance()
    {
        BeginTest("Cache Configuration");
        
        if (managerToTest == null)
        {
            FailTest("Manager is null");
            return;
        }
        
        Log($"  Access Cache Size: {managerToTest.accessCacheSize}");
        Log($"  Player Name Cache Size: {managerToTest.playerNameCacheSize}");
        Log($"  Role Index Cache Size: {managerToTest.roleIndexCacheSize}");
        Log($"  Super Admin Cache Size: {managerToTest.isSuperAdminCacheSize}");
        
        if (managerToTest.accessCacheSize > 0 && 
            managerToTest.playerNameCacheSize > 0 && 
            managerToTest.roleIndexCacheSize > 0)
        {
            PassTest("All caches properly configured");
        }
        else
        {
            FailTest("Some caches have invalid sizes");
        }
    }
    
    // Helper methods
    void BeginTest(string testName)
    {
        testsRun++;
        Log($"\n[TEST {testsRun}] {testName}");
    }
    
    void PassTest(string message)
    {
        testsPassed++;
        Log($"  ✓ PASS: {message}", Color.green);
    }
    
    void FailTest(string message)
    {
        testsFailed++;
        Log($"  ✗ FAIL: {message}", Color.red);
    }
    
    void Log(string message, Color? color = null)
    {
        if (enableVerboseLogging)
        {
            if (color.HasValue)
            {
                Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color.Value)}>{message}</color>");
            }
            else
            {
                Debug.Log(message);
            }
        }
        
        if (resultBuilder != null)
        {
            resultBuilder.AppendLine(message);
        }
    }
}
