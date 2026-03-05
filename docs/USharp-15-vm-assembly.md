# The Udon VM and Udon Assembly

**Source:** https://creators.vrchat.com/worlds/udon/vm-and-assembly/

> Community-written page. VRChat cannot guarantee accuracy.

---

## Overview of the Udon VM

The Udon VM is a bytecode interpreter that runs compiled Udon Graph (and UdonSharp) programs inside a .NET environment.

Key characteristics:
- No direct call/return — `JUMP_INDIRECT` can simulate subroutines.
- Flow control via `JUMP`, `JUMP_INDIRECT`, and `JUMP_IF_FALSE`.
- Can call allowed C# functions via `EXTERN`.
- **No local variables** — only fields on the object (the "heap").
- Has an integer stack, used primarily as "extra parameters" for opcodes.
- Recursive functions are possible but must be implemented very carefully (no locals means shared state).

> **Tip:** You can export Udon Assembly from Udon Graph and UdonSharp programs to see how your code compiles and to discover extern names.

---

## Udon Types

Udon uses its own type name system derived from .NET names. Rules:
- Remove all `.` and `+` characters: `VRC.SDKBase.VRCPlayerApi+TrackingData` → `VRCSDKBaseVRCPlayerApiTrackingData`
- Append `Array` for `[]`: `System.Int32[]` → `SystemInt32Array`

---

## Udon Assembly

Programs consist of two sections:

```asm
.data_start
    # Variable declarations
.data_end

.code_start
    # Opcodes
.code_end
```

### Data Section

Declares variables stored in the "Udon Heap" (a flat typed array).

```asm
message: %SystemString, "Hello, world!"
```

Format: `symbolName: %UdonType, initialValue`

- Initial value can be: `null`, `this`, `true`, `false`, string literal, char constant, integer, unsigned integer (suffix `u`), float.
- `this` meaning depends on variable type:
  - `GameObject` → the `UdonBehaviour`'s `GameObject`
  - `Transform` → `GameObject.transform`
  - `UdonBehaviour` / `IUdonBehaviour` / `Object` → the `UdonBehaviour` itself
- Export a variable: `.export message`
- Mark as synced: `.sync message, none` (interpolation modes: `none`, `linear`, `smooth`)

> **Known Udon Assembly limitations:** `SystemType`, `SystemInt64`, `SystemUInt64`, `SystemSByte`, `SystemByte`, `SystemInt16`, `SystemUInt16`, and `SystemBoolean` cannot be specified as non-null initializers. Floating-point numbers are always read as `float` even when declared as `double`.

### Code Section

A list of opcodes with labels and exports:

```asm
.export _start
_start:
    PUSH, message
    EXTERN, "UnityEngineDebug.__Log__SystemObject__SystemVoid"
    JUMP, 0xFFFFFFFC
```

- `.export _start` (or whatever symbol) marks the entry point for event handlers.
- Standard events begin with `_` and receive parameters through variables (not function args).
- First two events on load: `_onEnable` then `_start` — always run before anything else.
- Custom events do **not** start with `_` and take no parameters (by default).
- `JUMP, 0xFFFFFFFC` ends execution (return from Udon code).

> **Caution:** Two code symbols pointing to the same address cause an "Address aliasing detected" error. Use a `NOP` to separate them.

---

## Udon Opcodes

| Opcode | Code | Description |
|---|---|---|
| `NOP` | 0 | No operation. Use to workaround address aliasing errors. |
| `PUSH, param` | 1 | Pushes a heap address (integer) onto the stack. |
| `POP` | 2 | Removes the top integer from the stack. |
| `JUMP_IF_FALSE, param` | 4 | Pops a heap index, reads a `SystemBoolean`. If false, jumps to `param`. |
| `JUMP, param` | 5 | Unconditional jump. `JUMP, 0xFFFFFFFC` = return/end execution. |
| `EXTERN, param` | 6 | Calls an external C# function. `param` = heap index containing the extern name string (cached after first call). |
| `ANNOTATION, param` | 7 | Long NOP; parameter ignored. |
| `JUMP_INDIRECT, param` | 8 | Reads a `SystemUInt32` from the heap index at `param`, jumps to that bytecode position. |
| `COPY` | 9 | Pops two heap indexes; copies value from first-pushed to second-pushed. |

### EXTERN details

- Parameters are pushed in order before the EXTERN call.
- Non-static methods require the `this` parameter pushed first.
- Return value (if not `SystemVoid`) is written to the last pushed heap slot (treated as `out`).
- `ref` and `out` parameters are both read and written.

---

## Extern Signature Format

```
UdonTypeName.__MethodName__Param1UdonType_Param2UdonType__ReturnUdonType
```

Example: `SystemDateTimeOffset.__TryParseExact__SystemString_SystemStringArray_SystemIFormatProvider_SystemGlobalizationDateTimeStyles_SystemDateTimeOffsetRef__SystemBoolean`

= `System.DateTimeOffset.TryParseExact(string, string[], IFormatProvider, DateTimeStyles, out DateTimeOffset) → bool`

Rules:
- Always starts with `__`, then method name, then `__`.
- Constructor: method name is `ctor`.
- Parameters separated by `_`.
- `ref`/`out` parameters use `Ref` suffix on the Udon type name.
- `VRCUdonUdonBehaviour` becomes `VRCUdonCommonInterfacesIUdonEventReceiver`.
- Generics list type params as Udon type names and have invisible `SystemType` parameters.
- There is no complete public extern reference. Use Udon Graph or the [UdonSharp Class Exposure Tree](https://udonsharp.docs.vrchat.com/class-exposure-tree) to discover available externs.
