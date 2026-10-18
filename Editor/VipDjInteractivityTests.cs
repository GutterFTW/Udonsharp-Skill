#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// Exercises the real row-interactivity method and manager authority independently.
// The creator/Read-Only case fails before the creator override moves below target protection.
public class VipDjInteractivityTests
{
    private GameObject managerObject;
    private GameObject uiObject;
    private GameObject rowObject;
    private VipWhitelistManager manager;
    private VipWhitelistUI ui;
    private Toggle dj;
    private VRCPlayerApi creator;
    private Func<VRCPlayerApi> previousLocal;
    private Func<VRCPlayerApi> previousCreator;
    private Func<bool> previousIsCreator;
    private Func<VRCPlayerApi, int> previousPlayerId;

    [SetUp]
    public void SetUp()
    {
        previousLocal = Networking._LocalPlayer;
        previousCreator = Networking._GetInstanceOwner;
        previousIsCreator = Networking._IsInstanceOwner;
        previousPlayerId = VRCPlayerApi._GetPlayerId;
        creator = new VRCPlayerApi { displayName = "Creator", isLocal = true };
        creator.AddToList();
        VRCPlayerApi._GetPlayerId = player => ReferenceEquals(player, creator) ? 1 : 2;
        Networking._LocalPlayer = () => creator;
        Networking._GetInstanceOwner = () => creator;
        Networking._IsInstanceOwner = () => true;

        managerObject = new GameObject("DJ authority test");
        manager = managerObject.AddComponent<VipWhitelistManager>();
        manager.roleNames = new[] { "Protected" };
        manager.roleCanReadOnly = new[] { true };
        manager.roleCanVipAccess = new[] { false };
        manager.EnsureRoleBuffersInitialized();
        Set(manager, "roleMemberCount", new[] { 1 });
        var members = new string[256]; members[0] = "Protected Guest";
        var normalized = new string[256]; normalized[0] = "protected guest";
        Set(manager, "roleMembersFlat", members);
        Set(manager, "roleMembersFlatNorm", normalized);

        uiObject = new GameObject("DJ UI test");
        ui = uiObject.AddComponent<VipWhitelistUI>();
        ui.manager = manager;
        Set(ui, "_cachedManager", manager);
        rowObject = new GameObject("DJ row test");
        var row = rowObject.AddComponent<VipWhitelistRow>();
        var toggleObject = new GameObject("DJ", typeof(RectTransform));
        toggleObject.transform.SetParent(rowObject.transform);
        dj = toggleObject.AddComponent<Toggle>();
        row.djToggle = dj;
        row.cachedRoleIndex = 0;
        row.cachedRawName = "Protected Guest";
        Set(ui, "rowCount", 1);
        Set(ui, "rows", new[] { rowObject });
        Set(ui, "rowScripts", new[] { row });
        Set(ui, "rowKeys", new[] { "Protected Guest" });
    }

    [TearDown]
    public void TearDown()
    {
        creator?.RemoveFromList();
        Networking._LocalPlayer = previousLocal;
        Networking._GetInstanceOwner = previousCreator;
        Networking._IsInstanceOwner = previousIsCreator;
        VRCPlayerApi._GetPlayerId = previousPlayerId;
        if (rowObject != null) UnityEngine.Object.DestroyImmediate(rowObject);
        if (uiObject != null) UnityEngine.Object.DestroyImmediate(uiObject);
        if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void CreatorCannotInteractWithReadOnlyDjTargetDeniedByManager()
    {
        Assert.IsTrue(manager.CanLocalManageDj());
        Assert.IsFalse((bool)Call(manager, "CanLocalManageDjTarget", "Protected Guest"));
        Call(ui, "SetRowDjInteractable", rowObject, true, 0);
        Assert.IsFalse(dj.interactable, "Creator UI must reflect the existing manager target restriction.");
    }

    [Test]
    public void SuperAdminRetainsReadOnlyDjOverride()
    {
        Set(manager, "_superAdminWhitelistNormalized", new[] { "creator" });
        Assert.IsTrue((bool)Call(manager, "CanLocalManageDjTarget", "Protected Guest"));
        Call(ui, "SetRowDjInteractable", rowObject, true, 0);
        Assert.IsTrue(dj.interactable);
    }

    [Test]
    public void CreatorCanStillInteractWithUnprotectedDjTarget()
    {
        manager.roleCanReadOnly[0] = false;
        Assert.IsTrue((bool)Call(manager, "CanLocalManageDjTarget", "Protected Guest"));
        Call(ui, "SetRowDjInteractable", rowObject, true, 0);
        Assert.IsTrue(dj.interactable);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static object Call(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
#endif
