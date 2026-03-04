# UdonSharp Overview

**Source:** https://udonsharp.docs.vrchat.com/ | https://creators.vrchat.com/worlds/udon/udonsharp/

---

## What is UdonSharp?

UdonSharp is a compiler that converts C# source code into VRChat's Udon assembly bytecode.
It is **not** standard C# — it compiles a subset of the language for the Udon VM inside VRChat.

---

## Standard Required Usings

```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
```

---

## Supported C# Features

- Flow control: `if` `else` `while` `for` `do` `foreach` `switch` `return` `break` `continue`
- Ternary operator `condition ? a : b` and null coalescing `??`
- Implicit and explicit type conversions
- Arrays and array indexers
- All built-in arithmetic operators
- Conditional short-circuit: `true || CheckIfTrue()` will not call `CheckIfTrue()`
- `typeof()`
- Extern methods with `out` or `ref` parameters (e.g., `Physics.Raycast()`)
- User-defined methods with parameters, return values, `out`/`ref`, extension methods, `params`
- User-defined properties (get/set)
- Static user methods
- `UdonSharpBehaviour` inheritance, virtual methods, and overrides
- Unity/Udon event callbacks with arguments
- String interpolation `$"Hello {name}"`
- Field initializers
- Jagged arrays
- Cross-behaviour field access and method calls
- Recursive methods via `[RecursiveMethod]` attribute

---

## Differences from Regular Unity C#

| Topic | Regular C# | UdonSharp |
|---|---|---|
| Base class | `MonoBehaviour` | `UdonSharpBehaviour` |
| Collections | `List<T>`, `Dictionary<T>`, etc. | Arrays `[]` **only** |
| `GetComponent<UdonBehaviour>()` | Works normally | Must use `(UdonBehaviour)GetComponent(typeof(UdonBehaviour))` |
| Field initializers | Runtime | Compile time only; use `Start()` for scene-dependent init |
| Numeric casts | Unchecked | Overflow-checked |
| Struct mutating methods | In-place | Return value only: `myVec = myVec.normalized;` |
| Recursive methods | Automatic | Need `[RecursiveMethod]` attribute |
| `GetType()` on jagged arrays | Typed | Returns `object[]` |

---

## Udon Bugs to Know

- **Struct mutating methods do not modify in-place** — `myVec.Normalize()` has no effect.
  Use: `myVec = Vector3.Normalize(myVec);` or `myVec = myVec.normalized;`
- Numeric casts are overflow-checked — be careful with byte/short arithmetic.

---

## Script Setup

### From UdonBehaviour component:
1. Add an `UdonBehaviour` component to a GameObject.
2. Set program type to "Udon C# Program Asset".
3. Click "New Program", then "Create Script".

### From Asset Explorer:
1. Right-click in Project panel → Create → U# Script.
2. This creates a `.cs` file **and** an `UdonSharpProgramAsset`.

> ⚠️ **Each `.cs` file must be connected to exactly one `UdonSharpProgramAsset`.**
> Do not reassign the script on an asset that is already in use.

---

## Example: Minimal Behaviour

```csharp
using UdonSharp;
using UnityEngine;

public class RotatingCube : UdonSharpBehaviour
{
    private void Update()
    {
        transform.Rotate(Vector3.up, 90f * Time.deltaTime);
    }
}
```
