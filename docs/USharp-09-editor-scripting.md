# Editor Scripting

**Source:** https://udonsharp.docs.vrchat.com/editor-scripting

---

## Overview

UdonSharp creates a **proxy C# MonoBehaviour** for every UdonSharpBehaviour in the scene.
This proxy is:
- Disabled by default
- Hidden in the Inspector
- Used to store and transfer data between the editor and the Udon program asset

**Never access UdonSharpBehaviour components through standard `GetComponent<T>()` in editor scripts.**
Use the UdonSharp-specific editor utilities instead.

---

## Required Guard

All editor scripting code must be guarded:

```csharp
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharp.Editor;
// your editor-only using statements
#endif
```

---

## Core Editor Utilities

### Getting / Adding Components
```csharp
// Getting
MyBehaviour behaviour = targetGameObject.GetUdonSharpComponent<MyBehaviour>();
MyBehaviour[] behaviours = targetGameObject.GetUdonSharpComponentsInChildren<MyBehaviour>();

// Adding
MyBehaviour behaviour = targetGameObject.AddUdonSharpComponent<MyBehaviour>();
```

### Reading from a Proxy
Before reading field values from a stored proxy reference, sync it from the UdonBehaviour:
```csharp
behaviour.UpdateProxy();
Debug.Log(behaviour.myPublicField);
```

### Writing to a Proxy
After modifying fields on the proxy, push them back to the UdonBehaviour:
```csharp
behaviour.myPublicField = "new value";
behaviour.ApplyProxyModifications();
```

### Destroying
```csharp
UdonSharpEditorUtility.DestroyImmediate(behaviour);
```

---

## Custom Inspectors

Always start with the standard UdonSharp inspector header:

```csharp
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;

[CustomEditor(typeof(MyBehaviour))]
public class MyBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ALWAYS call this first — handles compile errors and inspector header
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

        MyBehaviour behaviour = (MyBehaviour)target;
        behaviour.UpdateProxy();

        // Draw custom fields
        EditorGUI.BeginChangeCheck();
        float newVal = EditorGUILayout.FloatField("My Field", behaviour.myField);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(behaviour, "Change My Field");
            behaviour.myField = newVal;
            behaviour.ApplyProxyModifications();
        }
    }
}
#endif
```

---

## Gizmos

Gizmos can be drawn by implementing `OnDrawGizmos` inside the guard block:

```csharp
#if !COMPILER_UDONSHARP && UNITY_EDITOR
private void OnDrawGizmos()
{
    this.UpdateProxy();
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireSphere(transform.position, myRange);
}
#endif
```

> ⚠️ `this.UpdateProxy()` is an extension method — you need `using UdonSharpEditor;`

---

## Full Example

```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class TeleportZone : UdonSharpBehaviour
{
    [SerializeField] public float radius = 3f;
    [SerializeField] public Transform destination;

    private void OnTriggerEnter(Collider other)
    {
        if (!Networking.IsOwner(gameObject)) return;
        Networking.LocalPlayer.TeleportTo(
            destination.position,
            destination.rotation,
            VRC_SceneDescriptor.SpawnOrientation.Default,
            false
        );
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    private void OnDrawGizmos()
    {
        this.UpdateProxy();
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(transform.position, radius);
        if (destination != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, destination.position);
        }
    }
#endif
}
```
