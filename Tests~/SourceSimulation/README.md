# VIP manager source simulation

Run from the asset repository with .NET SDK 10:

```sh
dotnet run --project 'Tests~/SourceSimulation/SourceSimulation.csproj' --configuration Release
```

This compiles the actual `VipWhitelistManager.cs` and its linked editor regression cases against small SDK/engine adapters. There are no downloaded test packages, private paths, credentials, or generated Unity files. A failed assertion, setup, teardown, or zero executed cases returns a failing exit code.

The adapters control creator identity and object ownership independently. Delayed callbacks and ownership confirmations are invoked by the tests; there is no real transport or automatic Unity lifecycle. The UI adapter only receives manager notifications. This runner therefore verifies manager state/permission logic and does not execute panel/interactivity fixtures.

`Tests~` is excluded from Unity asset import. The engine adapters and their small assertion API do not ship in the world. The same linked regression cases can also run with the actual SDK in Unity's editor test runner.

Passing here does **not** establish Unity import, Udon compilation, ClientSim UI behavior, prefab references, or live multiplayer correctness. Those checks remain separate in `TEST_REPORT.md` and the README acceptance checklist.
