# Persistence

**Source:** https://creators.vrchat.com/worlds/udon/persistence/

---

## Overview

Persistence lets you save per-player data across world visits.
Data persists between sessions in the same world for the same player.

There are two persistence systems:
1. **Player Data** — key/value store per player, accessible via `VRCPlayerApi`
2. **Player Objects** — GameObjects owned/managed per player with `VRCEnablePersistence` component

---

## Player Data

Player Data is stored per-player in VRChat's cloud. The local player's data is loaded when they join
a world and saved when they leave.

> ⚠️ **Limits:** Total per-player storage is **100 KB**. String values can be up to **50 KB**.

### Reading Data
```csharp
// All reads/writes are always for the LOCAL PLAYER only
string value;
if (Networking.LocalPlayer.TryGetPlayerData(out value, "myKey"))
{
    Debug.Log("Got value: " + value);
}
else
{
    Debug.Log("Key not found, using default.");
}
```

### Writing Data
```csharp
Networking.LocalPlayer.SetPlayerData("myKey", "myValue");
Networking.LocalPlayer.SetPlayerData("score", score.ToString());
Networking.LocalPlayer.SetPlayerData("position", JsonUtility.ToJson(transform.position));
```

### Deleting Data
```csharp
Networking.LocalPlayer.DeletePlayerData("myKey");
```

### Detecting Changes
```csharp
public override void OnPlayerDataUpdated(VRCPlayerApi player)
{
    // Fires when the player's data is loaded or changed
    if (!player.isLocal) return;

    string value;
    if (player.TryGetPlayerData(out value, "myKey"))
    {
        // apply the loaded value
    }
}
```

---

## Player Data: DataList/DataDictionary Integration

Convert complex data to/from JSON when storing:
```csharp
void SaveProgress()
{
    DataDictionary save = new DataDictionary();
    save.SetValue("level", new DataToken(currentLevel));
    save.SetValue("coins", new DataToken(coins));

    string json;
    VRCJson.TrySerializeToJson(new DataToken(save), JsonExportType.Minify, out json);
    Networking.LocalPlayer.SetPlayerData("progress", json);
}

void LoadProgress()
{
    string json;
    if (!Networking.LocalPlayer.TryGetPlayerData(out json, "progress")) return;

    DataToken token;
    if (!VRCJson.TryDeserializeFromJson(json, out token)) return;

    DataDictionary save = token.DataDictionary;
    currentLevel = save["level"].Int;
    coins = save["coins"].Int;
}
```

---

## Player Objects

Player Objects are GameObjects that are automatically assigned to each player.
They persist position, rotation, and synced variables between sessions.

### Setup
1. Add a `VRCEnablePersistence` component to a root GameObject in the scene.
2. The GameObject (and its children) will be cloned per-player automatically.
3. UdonBehaviours on the object can use `[UdonSynced]` as normal.

### Ownership
- Each Player Object is automatically owned by its associated player.
- The player that owns it is the one whose data it persists.

### Notes
- Position/Rotation are persisted automatically.
- `[UdonSynced]` variables on Player Objects are also persisted.
- Access a specific player's object via finding GameObjects by name or caching during `OnPlayerJoined`.
- Player Objects are created/loaded when the player joins, and saved when they leave.
