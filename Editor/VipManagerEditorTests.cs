using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System.Reflection;

/// <summary>
/// Editor tests for the VIP Manager system.
/// Tests configuration, validation, and initialization without requiring play mode.
/// </summary>
public class VipManagerEditorTests
{
    private GameObject managerGameObject;
    private VipWhitelistManager manager;

    [SetUp]
    public void Setup()
    {
        // Create a new GameObject with VipWhitelistManager for each test
        managerGameObject = new GameObject("Test VIP Manager");
        manager = managerGameObject.AddComponent<VipWhitelistManager>();
        
        // Configure with test data
        manager.roleNames = new string[] { "Admin", "DJ", "VIP", "Staff" };
        manager.roleColors = new Color[] { Color.red, Color.blue, Color.yellow, Color.green };
        manager.superAdminWhitelist = new string[] { "TestAdmin", "TestOwner" };
        manager.enableDebugLogs = true;
        manager.accessCacheSize = 128;
        manager.playerNameCacheSize = 128;
        manager.roleIndexCacheSize = 128;
        manager.isSuperAdminCacheSize = 128;
    }

    [TearDown]
    public void Teardown()
    {
        // Clean up after each test
        if (managerGameObject != null)
        {
            Object.DestroyImmediate(managerGameObject);
        }
    }

    [Test]
    public void TestManagerInitialization()
    {
        Assert.IsNotNull(manager, "VipWhitelistManager should be created");
        Assert.IsNotNull(manager.roleNames, "Role names should be initialized");
        Assert.AreEqual(4, manager.roleNames.Length, "Should have 4 roles configured");
        Debug.Log("<color=green>✓ PASS:</color> Manager Initialization");
    }

    [Test]
    public void TestRoleArrayConsistency()
    {
        int roleCount = manager.roleNames.Length;
        
        Assert.AreEqual(roleCount, manager.roleColors.Length, "roleColors should match roleNames length");
        
        Debug.Log($"<color=green>✓ PASS:</color> Role Array Consistency - {roleCount} roles");
    }

    [Test]
    public void TestSuperAdminConfiguration()
    {
        Assert.IsNotNull(manager.superAdminWhitelist, "Super admin whitelist should exist");
        Assert.AreEqual(2, manager.superAdminWhitelist.Length, "Should have 2 super admins");
        Assert.AreEqual("TestAdmin", manager.superAdminWhitelist[0], "First super admin should be TestAdmin");
        
        Debug.Log("<color=green>✓ PASS:</color> Super Admin Configuration");
    }

    [Test]
    public void TestCacheConfiguration()
    {
        Assert.AreEqual(128, manager.accessCacheSize, "Access cache should be 128");
        Assert.AreEqual(128, manager.playerNameCacheSize, "Player name cache should be 128");
        Assert.AreEqual(128, manager.roleIndexCacheSize, "Role index cache should be 128");
        Assert.AreEqual(128, manager.isSuperAdminCacheSize, "Super admin cache should be 128");
        
        Debug.Log("<color=green>✓ PASS:</color> Cache Configuration");
    }

    [Test]
    public void TestRoleNamesNotNull()
    {
        foreach (var roleName in manager.roleNames)
        {
            Assert.IsNotNull(roleName, "Role name should not be null");
            Assert.IsNotEmpty(roleName, "Role name should not be empty");
        }
        
        Debug.Log($"<color=green>✓ PASS:</color> All {manager.roleNames.Length} role names are valid");
    }

    [Test]
    public void TestComponentReferences()
    {
        // Test that the manager component can be found
        var foundManager = managerGameObject.GetComponent<VipWhitelistManager>();
        Assert.IsNotNull(foundManager, "Should be able to find VipWhitelistManager component");
        Assert.AreEqual(manager, foundManager, "Found manager should be the same instance");
        
        Debug.Log("<color=green>✓ PASS:</color> Component References");
    }

    [Test]
    public void TestDebugLogConfiguration()
    {
        Assert.IsTrue(manager.enableDebugLogs, "Debug logs should be enabled");
        Assert.IsNotNull(manager.logColor, "Log color should be configured");
        
        Debug.Log("<color=green>✓ PASS:</color> Debug Log Configuration");
    }

    [Test]
    public void TestRoleColorConfiguration()
    {
        Assert.AreEqual(Color.red, manager.roleColors[0], "Admin role should be red");
        Assert.AreEqual(Color.blue, manager.roleColors[1], "DJ role should be blue");
        Assert.AreEqual(Color.yellow, manager.roleColors[2], "VIP role should be yellow");
        Assert.AreEqual(Color.green, manager.roleColors[3], "Staff role should be green");
        
        Debug.Log("<color=green>✓ PASS:</color> Role Color Configuration");
    }

    [Test]
    public void TestSuperAdminColorConfiguration()
    {
        Assert.IsNotNull(manager.superAdminNameColor, "Super admin color should be configured");
        
        Debug.Log($"<color=green>✓ PASS:</color> Super Admin Color: RGB({manager.superAdminNameColor.r}, {manager.superAdminNameColor.g}, {manager.superAdminNameColor.b})");
    }

    [Test]
    public void TestPublicMethodsExist()
    {
        // Use reflection to check that key public methods exist
        var type = typeof(VipWhitelistManager);
        
        Assert.IsNotNull(type.GetMethod("IsAuthed", BindingFlags.Public | BindingFlags.Instance), 
            "IsAuthed method should exist");
        Assert.IsNotNull(type.GetMethod("IsDj", BindingFlags.Public | BindingFlags.Instance), 
            "IsDj method should exist");
        Assert.IsNotNull(type.GetMethod("IsSuperAdmin", BindingFlags.Public | BindingFlags.Instance), 
            "IsSuperAdmin method should exist");
        
        Debug.Log("<color=green>✓ PASS:</color> All core public methods exist");
    }
}
