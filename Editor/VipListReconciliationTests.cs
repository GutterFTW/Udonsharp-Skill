#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VRC.SDKBase;

public class VipListReconciliationTests
{
    private GameObject managerObject;
    private GameObject uiObject;
    private GameObject rowObject;
    private VipWhitelistManager manager;
    private VipWhitelistUI ui;
    private VRCPlayerApi presentPlayer;
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
        presentPlayer = new VRCPlayerApi { displayName = "Present Guest", isLocal = true };
        presentPlayer.AddToList();
        Networking._LocalPlayer = () => presentPlayer;
        Networking._GetInstanceOwner = () => null;
        Networking._IsInstanceOwner = () => false;
        VRCPlayerApi._GetPlayerId = player => ReferenceEquals(player, presentPlayer) ? 1 : 2;
        managerObject = new GameObject("List authority test");
        manager = managerObject.AddComponent<VipWhitelistManager>();
        manager.roleNames = new[] { "Retained role" };
        manager.roleCanReadOnly = new[] { false };
        manager.EnsureRoleBuffersInitialized();
        uiObject = new GameObject("List UI test");
        ui = uiObject.AddComponent<VipWhitelistUI>();
        ui.manager = manager;
        Set(ui, "_cachedManager", manager);
    }

    [TearDown]
    public void TearDown()
    {
        presentPlayer?.RemoveFromList();
        Networking._LocalPlayer = previousLocal;
        Networking._GetInstanceOwner = previousCreator;
        Networking._IsInstanceOwner = previousIsCreator;
        VRCPlayerApi._GetPlayerId = previousPlayerId;
        if (rowObject != null) UnityEngine.Object.DestroyImmediate(rowObject);
        if (uiObject != null) UnityEngine.Object.DestroyImmediate(uiObject);
        if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SameCountVipOrDjReplacementSchedulesRebuild(bool djList)
    {
        string field = djList ? "syncedDj" : "syncedManual";
        string count = djList ? "syncedDjCount" : "syncedManualCount";
        var membership = new[] { "offline a" };
        Set(manager, field, membership);
        Set(manager, count, 1);
        manager.ProcessDeferredListRebuild();
        Assert.IsFalse((bool)Get(manager, "_rebuildScheduled"));
        // Mutate the same array to ensure the last-notified snapshot is a copy.
        membership[0] = "offline b";
        manager.NotifyLists();
        Assert.IsTrue((bool)Get(manager, "_rebuildScheduled"), "A different member at the same count requires row reconciliation.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RevokedOfflineVipOrDjRowIsPruned(bool djList)
    {
        SeedRow("offline guest");
        string field = djList ? "syncedDj" : "syncedManual";
        string count = djList ? "syncedDjCount" : "syncedManualCount";
        Set(manager, field, new[] { "offline guest" });
        Set(manager, count, 1);
        Assert.IsTrue(manager.PlayerHasUiPresence("offline guest"));
        Set(manager, field, new string[0]);
        Set(manager, count, 0);
        ui.RebuildPlayerList();
        int rows = (int)Get(ui, "rowCount");
        var keys = (string[])Get(ui, "rowKeys");
        for (int i = 0; i < rows; i++)
            Assert.AreNotEqual("offline guest", keys[i], "Revocation after departure must release the stale offline row, even if its pooled object is reused for a present player.");
    }

    [TestCase("present guest", "present")]
    [TestCase("offline guest", "role")]
    [TestCase("offline guest", "superadmin")]
    [TestCase("offline guest", "creator")]
    [TestCase("offline guest", "other-manual")]
    public void ReconciliationPreservesRowsWithAnotherPresenceReason(string name, string reason)
    {
        SeedRow(name);
        if (reason == "role")
        {
            var members = new string[256]; members[0] = name;
            Set(manager, "roleMembersFlat", members);
            Set(manager, "roleMembersFlatNorm", (string[])members.Clone());
            Set(manager, "roleMemberCount", new[] { 1 });
        }
        if (reason == "superadmin") Set(manager, "_superAdminWhitelistNormalized", new[] { name });
        if (reason == "creator") Set(manager, "initialOwner", name);
        if (reason == "other-manual")
        {
            Set(manager, "syncedDj", new[] { name });
            Set(manager, "syncedDjCount", 1);
        }
        ui.RebuildPlayerList();
        Assert.AreEqual(1, (int)Get(ui, "rowCount"));
        Assert.AreEqual(name, ((string[])Get(ui, "rowKeys"))[0]);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SameCountReplacementReusesFullCapacityWithoutRemovingCaseVariedPresentRow(bool djList)
    {
        SeedRow("offline a");
        var firstRow = rowObject.GetComponent<VipWhitelistRow>();
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(uiObject.transform);
        ui.contentRoot = content.transform;
        var template = new GameObject("Row template");
        template.transform.SetParent(uiObject.transform);
        template.AddComponent<VipWhitelistRow>();
        ui.rowTemplate = template;
        var presentObject = new GameObject("Present row");
        presentObject.transform.SetParent(content.transform);
        var presentRow = presentObject.AddComponent<VipWhitelistRow>();
        presentRow.cachedRawName = "present guest";
        presentRow.cachedRawNameLower = "present guest";
        Set(ui, "rows", new[] { rowObject, presentObject });
        Set(ui, "rowScripts", new[] { firstRow, presentRow });
        Set(ui, "rowKeys", new[] { "offline a", "present guest" });
        Set(ui, "rowKeysLower", new[] { "offline a", "present guest" });
        Set(ui, "rowCount", 2);
        manager.lists = new[] { ui };
        string field = djList ? "syncedDj" : "syncedManual";
        string count = djList ? "syncedDjCount" : "syncedManualCount";
        var membership = new[] { "offline a" };
        Set(manager, field, membership);
        Set(manager, count, 1);
        manager.ProcessDeferredListRebuild();
        membership[0] = "offline b";
        manager.NotifyLists();
        Assert.IsTrue((bool)Get(manager, "_rebuildScheduled"));
        manager.ProcessDeferredListRebuild();

        Assert.AreEqual(2, (int)Get(ui, "rowCount"));
        CollectionAssert.AreEquivalent(new[] { "offline b", "present guest" }, (string[])Get(ui, "rowKeys"));
        CollectionAssert.Contains((GameObject[])Get(ui, "rows"), presentObject);
        CollectionAssert.Contains((GameObject[])Get(ui, "rows"), rowObject, "The revoked row's slot and pooled object should be reused for the replacement.");
    }

    private void SeedRow(string name)
    {
        rowObject = new GameObject("Reconciliation row");
        var row = rowObject.AddComponent<VipWhitelistRow>();
        row.cachedRawName = name;
        row.cachedRawNameLower = name;
        Set(ui, "rows", new[] { rowObject });
        Set(ui, "rowScripts", new[] { row });
        Set(ui, "rowKeys", new[] { name });
        Set(ui, "rowKeysLower", new[] { name });
        Set(ui, "rowCount", 1);
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
#endif
