#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VRC.SDKBase;

// SDK hooks let these tests vary creator identity separately from object ownership.
// They exercise the C# permission/recovery paths; multiplayer transport still needs VRChat.
public class VipInitialOwnerTests
{
    private GameObject managerObject;
    private VipWhitelistManager manager;
    private VRCPlayerApi creator;
    private VRCPlayerApi staff;
    private VRCPlayerApi localPlayer;
    private VRCPlayerApi instanceOwner;
    private bool localIsCreator;
    private bool localOwnsObject;
    private Func<VRCPlayerApi> previousLocalPlayer;
    private Func<VRCPlayerApi> previousInstanceOwner;
    private Func<bool> previousIsInstanceOwner;
    private Func<bool> previousIsMaster;
    private Func<VRCPlayerApi, GameObject, bool> previousIsOwner;
    private Func<VRCPlayerApi, int> previousPlayerId;
    private Action<VRCPlayerApi, GameObject> previousSetOwner;

    [SetUp]
    public void SetUp()
    {
        previousLocalPlayer = Networking._LocalPlayer;
        previousInstanceOwner = Networking._GetInstanceOwner;
        previousIsInstanceOwner = Networking._IsInstanceOwner;
        previousIsMaster = Networking._IsMaster;
        previousIsOwner = Networking._IsOwner;
        previousPlayerId = VRCPlayerApi._GetPlayerId;
        previousSetOwner = Networking._SetOwner;

        managerObject = new GameObject("Creator recovery test");
        manager = managerObject.AddComponent<VipWhitelistManager>();
        creator = new VRCPlayerApi { displayName = "Original Creator", isLocal = true };
        staff = new VRCPlayerApi { displayName = "Other Staff", isLocal = false };
        creator.AddToList();
        staff.AddToList();
        localPlayer = creator;
        instanceOwner = creator;
        localIsCreator = true;
        localOwnsObject = false;
        Networking._LocalPlayer = () => localPlayer;
        Networking._GetInstanceOwner = () => instanceOwner;
        Networking._IsInstanceOwner = () => localIsCreator;
        Networking._IsMaster = () => localOwnsObject;
        Networking._IsOwner = (player, obj) => localOwnsObject;
        VRCPlayerApi._GetPlayerId = player => ReferenceEquals(player, creator) ? 1 : 2;
        // Model a transfer request that has not yet been confirmed.
        Networking._SetOwner = (player, obj) => { };
    }

    [TearDown]
    public void TearDown()
    {
        creator?.RemoveFromList();
        staff?.RemoveFromList();
        Networking._LocalPlayer = previousLocalPlayer;
        Networking._GetInstanceOwner = previousInstanceOwner;
        Networking._IsInstanceOwner = previousIsInstanceOwner;
        Networking._IsMaster = previousIsMaster;
        Networking._IsOwner = previousIsOwner;
        VRCPlayerApi._GetPlayerId = previousPlayerId;
        Networking._SetOwner = previousSetOwner;
        if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void CreatorCanManageWithoutSyncedNameOrObjectOwnership()
    {
        Assert.IsTrue(manager.IsInstanceInitialOwner(creator));
        Assert.IsTrue(manager.IsInstanceInitialOwner("Original Creator"));
        Assert.IsTrue(manager.CanLocalManageDj());
        Assert.IsFalse(manager.IsInstanceInitialOwner(staff));
        Assert.IsTrue(string.IsNullOrEmpty(GetField<string>("initialOwner")));
    }

    [Test]
    public void LocalCreatorFlagWorksBeforeInstanceOwnerPlayerIsAvailable()
    {
        instanceOwner = null;
        Assert.IsTrue(manager.IsInstanceInitialOwner(creator));
        Assert.IsTrue(manager.CanLocalManageDj());
        Assert.AreEqual("original creator", manager.GetInitialOwnerName());
    }

    [Test]
    public void EmptySnapshotCannotEraseObservedCreator()
    {
        Assert.AreEqual("original creator", manager.GetInitialOwnerName());
        instanceOwner = null;
        localIsCreator = false;
        SetField("initialOwner", "");
        manager.OnDeserialization();
        Assert.IsTrue(manager.CanLocalManageDj());
        Assert.IsTrue(manager.PlayerHasUiPresence("Original Creator"));
        Assert.IsFalse(manager.IsInstanceInitialOwner(staff));
    }

    [Test]
    public void SyncedCreatorIsRetainedWhileCreatorIsAbsent()
    {
        instanceOwner = null;
        localIsCreator = false;
        SetField("initialOwner", "original creator");
        Assert.AreEqual("original creator", manager.GetInitialOwnerName());
        SetField("initialOwner", "");
        Assert.IsTrue(manager.IsInstanceInitialOwner("Original Creator"));
    }

    [Test]
    public void CreatorGrantSurvivesStaleSnapshotBeforeTransferCompletes()
    {
        manager.DjAdd("Guest DJ");
        Assert.AreEqual(0, manager.GetSyncedDjCount());
        SetField("initialOwner", "");
        manager.OnDeserialization();
        localOwnsObject = true;
        manager.OnOwnershipTransferred(creator);
        Assert.AreEqual("original creator", GetField<string>("initialOwner"));
        Assert.IsTrue(manager.IsDj("Guest DJ"));
        manager.DjRemove("Guest DJ");
        Assert.IsFalse(manager.IsDj("Guest DJ"));
    }

    [Test]
    public void ObjectOwnerDoesNotInheritCreatorPermissions()
    {
        localPlayer = staff;
        staff.isLocal = true;
        creator.isLocal = false;
        localIsCreator = false;
        localOwnsObject = true;
        manager.OnOwnershipTransferred(staff);
        Assert.AreEqual("original creator", manager.GetInitialOwnerName());
        Assert.IsFalse(manager.CanLocalManageDj());
        manager.DjAdd("Guest DJ");
        Assert.AreEqual(0, manager.GetSyncedDjCount());
    }

    [Test]
    public void UnidentifiedCreatorDoesNotFallBackToObjectOwner()
    {
        instanceOwner = null;
        localIsCreator = false;
        localOwnsObject = true;
        manager.OnOwnershipTransferred(creator);
        Assert.IsTrue(string.IsNullOrEmpty(manager.GetInitialOwnerName()));
        Assert.IsTrue(string.IsNullOrEmpty(GetField<string>("initialOwner")));
        Assert.IsFalse(manager.CanLocalManageDj());
    }

    [Test]
    public void LiveCreatorCorrectsStaleNameAndDeferredIdentity()
    {
        SetField("initialOwner", "other staff");
        instanceOwner = null;
        localIsCreator = false;
        manager.OnOwnershipTransferred(creator);
        instanceOwner = creator;
        localIsCreator = true;
        localOwnsObject = true;
        manager.ProcessInitialOwnerRefresh();
        Assert.AreEqual("original creator", GetField<string>("initialOwner"));
        Assert.IsFalse(manager.IsInstanceInitialOwner("Other Staff"));
        Assert.IsTrue(manager.CanLocalManageDj());
    }

    [Test]
    public void FailedSerializationSchedulesRetryEvenAfterDirtyFlagsCleared()
    {
        localOwnsObject = true;
        manager.OnPostSerialization(new VRC.Udon.Common.SerializationResult(false, 0));
        Assert.IsTrue(GetField<bool>("_serializationFailed"));
        Assert.IsTrue(GetField<bool>("_serializationRetryScheduled"));
    }

    [Test]
    public void SuccessfulSerializationDoesNotScheduleRetry()
    {
        localOwnsObject = true;
        manager.OnPostSerialization(new VRC.Udon.Common.SerializationResult(true, 32));
        Assert.IsFalse(GetField<bool>("_serializationFailed"));
        Assert.IsFalse(GetField<bool>("_serializationRetryScheduled"));
    }

    [Test]
    public void FailedSerializationDoesNotRetryAfterLosingOwnership()
    {
        localOwnsObject = false;
        manager.OnPostSerialization(new VRC.Udon.Common.SerializationResult(false, 0));
        Assert.IsFalse(GetField<bool>("_serializationFailed"));
        Assert.IsFalse(GetField<bool>("_serializationRetryScheduled"));
    }

    [Test]
    public void DjSystemEditWithoutCreatorIdentityWaitsForOwnership()
    {
        ConfigureAdminWithoutCreatorIdentity();
        manager.SetDjSystemEnabledState(false);
        Assert.IsTrue(manager.GetSyncedDjSystemEnabled());
        Assert.IsTrue(GetField<bool>("_pendingDjSystemChange"));
        manager.OnDeserialization();
        Assert.IsTrue(manager.GetSyncedDjSystemEnabled());
        localOwnsObject = true;
        manager.OnOwnershipTransferred(creator);
        Assert.IsFalse(manager.GetSyncedDjSystemEnabled());
        Assert.IsFalse(GetField<bool>("_pendingDjSystemChange"));
    }

    [Test]
    public void LatestPendingDjSystemIntentWinsEvenWhenMatchingCurrentState()
    {
        ConfigureAdminWithoutCreatorIdentity();
        manager.SetDjSystemEnabledState(false);
        manager.SetDjSystemEnabledState(true);
        localOwnsObject = true;
        manager.ProcessPendingEditApplication();
        Assert.IsTrue(manager.GetSyncedDjSystemEnabled());
        Assert.IsFalse(GetField<bool>("_pendingDjSystemChange"));
    }

    [Test]
    public void PendingDjSystemEditRechecksPermissionsOnTransfer()
    {
        ConfigureAdminWithoutCreatorIdentity();
        manager.SetDjSystemEnabledState(false);
        manager.superAdminWhitelist = new string[0];
        SetField("_roleBuffersFixed", false);
        localOwnsObject = true;
        manager.OnOwnershipTransferred(creator);
        Assert.IsFalse(manager.CanLocalManageDj());
        Assert.IsTrue(manager.GetSyncedDjSystemEnabled());
        Assert.IsFalse(GetField<bool>("_pendingDjSystemChange"));
    }

    [Test]
    public void OwnerCanChangeDjSystemWithoutCreatorIdentity()
    {
        ConfigureAdminWithoutCreatorIdentity();
        localOwnsObject = true;
        manager.SetDjSystemEnabledState(false);
        Assert.IsFalse(manager.GetSyncedDjSystemEnabled());
        manager.SetDjSystemEnabledState(true);
        Assert.IsTrue(manager.GetSyncedDjSystemEnabled());
    }

    [Test]
    public void DirectOwnerEditReplacesPendingDjSystemIntent()
    {
        ConfigureAdminWithoutCreatorIdentity();
        manager.SetDjSystemEnabledState(false);
        localOwnsObject = true;
        manager.SetDjSystemEnabledState(true);
        manager.ProcessPendingEditApplication();
        Assert.IsTrue(manager.GetSyncedDjSystemEnabled());
        Assert.IsFalse(GetField<bool>("_pendingDjSystemChange"));
    }

    private void ConfigureAdminWithoutCreatorIdentity()
    {
        instanceOwner = null;
        localIsCreator = false;
        manager.superAdminWhitelist = new string[] { "Original Creator" };
    }

    [Test]
    public void DjGrantsWithoutCreatorIdentitySurviveOwnershipHandoff()
    {
        ConfigureAdminWithoutCreatorIdentity();
        manager.DjAdd("Guest DJ");
        Assert.IsFalse(manager.IsDj("Guest DJ"));
        manager.OnDeserialization();
        localOwnsObject = true;
        manager.OnOwnershipTransferred(creator);
        Assert.IsTrue(manager.IsDj("Guest DJ"));
        manager.DjRemove("Guest DJ");
        Assert.IsFalse(manager.IsDj("Guest DJ"));
        Assert.IsTrue(string.IsNullOrEmpty(manager.GetInitialOwnerName()));
    }

    private T GetField<T>(string name)
    {
        return (T)typeof(VipWhitelistManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
    }

    private void SetField(string name, object value)
    {
        typeof(VipWhitelistManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, value);
    }
}
#endif
