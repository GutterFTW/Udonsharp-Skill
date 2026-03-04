# Data Containers

**Source:** https://creators.vrchat.com/worlds/udon/data-containers/

---

## Overview

Data Containers provide JSON-like data structures in Udon without needing `List<T>` or `Dictionary<T>`.
Available in VRC SDK3.

There are three main types:
- `DataList` — ordered list of `DataToken` values
- `DataDictionary` — key/value store with `DataToken` keys and values
- `DataToken` — wrapper for any supported value

---

## DataToken

A `DataToken` can hold any of these types:
- `null` (`TokenType.Null`)
- `bool`
- `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`
- `float`, `double`
- `string`
- `DataList`
- `DataDictionary`
- `DataError`

### Creating Tokens
```csharp
DataToken intToken    = new DataToken(42);
DataToken strToken    = new DataToken("hello");
DataToken boolToken   = new DataToken(true);
DataToken floatToken  = new DataToken(3.14f);
DataToken nullToken   = DataToken.Null;
```

### Reading Tokens
```csharp
DataToken t = ...;

if (t.TokenType == TokenType.Int)
    int value = t.Int;

if (t.TokenType == TokenType.String)
    string s = t.String;

// Available properties: .Boolean, .SByte, .Byte, .Short, .UShort,
// .Int, .UInt, .Long, .ULong, .Float, .Double, .String,
// .DataList, .DataDictionary, .Error
```

---

## DataList

```csharp
// Create
DataList list = new DataList();

// Add items
list.Add(new DataToken("item1"));
list.Add(new DataToken(99));

// Access
int count = list.Count;
DataToken first = list[0];

// Iteration
for (int i = 0; i < list.Count; i++)
{
    DataToken item = list[i];
}

// Check / search
bool contains = list.Contains(new DataToken("item1"));
int index = list.IndexOf(new DataToken(99));

// Remove
list.RemoveAt(0);
list.Remove(new DataToken("item1"));

// Other
list.Insert(0, new DataToken("inserted"));
DataList copy = list.DeepClone();
string[] strArray = list.ToStringArray();   // convert to string[]
int[] intArray    = list.ToIntArray();
```

---

## DataDictionary

```csharp
// Create
DataDictionary dict = new DataDictionary();

// Set values
dict.SetValue("name", new DataToken("Alice"));
dict.SetValue("score", new DataToken(100));
dict["level"] = new DataToken(5);

// Get values
DataToken val;
if (dict.TryGetValue("name", out val))
    string name = val.String;

DataToken directVal = dict["name"];  // returns null DataToken if missing

// Check
bool has = dict.ContainsKey("name");
int count = dict.Count;

// Keys / Values
DataList keys   = dict.GetKeys();
DataList values = dict.GetValues();

// Remove
dict.Remove("name");

// Deep clone
DataDictionary copy = dict.DeepClone();
```

---

## VRCJson

### Deserialize JSON
```csharp
string json = "{\"name\": \"Alice\", \"score\": 42}";
DataToken result;
if (!VRCJson.TryDeserializeFromJson(json, out result))
{
    Debug.LogError("Failed to parse JSON: " + result.Error);
    return;
}
DataDictionary data = result.DataDictionary;
string name = data["name"].String;
int score = data["score"].Int;
```

### Serialize to JSON
```csharp
DataDictionary data = new DataDictionary();
data.SetValue("x", new DataToken(1.5f));
data.SetValue("y", new DataToken(2.5f));

string json;
VRCJson.TrySerializeToJson(new DataToken(data), JsonExportType.Beautify, out json);
// or JsonExportType.Minify for compact output
```

---

## Combined Example: Remote Config via String Download

```csharp
[SerializeField] private VRCUrl configUrl;

void Start()
{
    VRCStringDownloader.LoadUrl(configUrl, (IUdonEventReceiver)this);
}

public override void OnStringLoadSuccess(IVRCStringDownload result)
{
    DataToken token;
    if (!VRCJson.TryDeserializeFromJson(result.Result, out token))
    {
        Debug.LogError("JSON parse error: " + token.Error);
        return;
    }
    DataDictionary config = token.DataDictionary;
    float volume = config["volume"].Float;
    string message = config["message"].String;
}

public override void OnStringLoadError(IVRCStringDownload result)
{
    Debug.LogError("String load error: " + result.Error);
}
```
