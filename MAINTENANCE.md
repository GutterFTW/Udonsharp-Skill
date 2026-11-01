# Maintainer guide and validation ledger

This repository contains a legacy VIP Manager asset and a reusable UdonSharp
reference library. The repository name does not make it an installable agent
skill or a standalone Unity project. This guide was reconciled against runtime
source at `a764cfddad8d8ef0499bd998d7d0b8b8390923d5` on 2026-10-31.
Documentation-only changes after that source revision do not establish runtime
acceptance.

## Read and change the right authority

| Document | Responsibility |
|---|---|
| [CONTRACT.md](CONTRACT.md) | Existing product/integration requirements, public API and access rules |
| [DEFINITIONS.md](DEFINITIONS.md) | Component and state vocabulary |
| [NETWORKING.md](NETWORKING.md) | Current synchronization flow and its limitations |
| [README.md](README.md) | World-builder setup and usage |
| This guide | Source navigation, known conformance gaps, maintenance and acceptance |
| [USharp-README.md](USharp-README.md) | How to reuse the reference documents and snippets |
| [TEST_REPORT.md](TEST_REPORT.md) | Historical test claims, not a current release certificate |

Keep intended rules separate from observed code. Record an implementation gap
when they differ; do not weaken a requirement to make the existing source look
compliant. Changes to roles, authority, identity, persistence or distribution
terms require the owner's decision and coordinated contract documentation before
implementation. A generic reference example is not a new VIP product rule.

## Source navigation

Use Graphify with repository `GutterFTW/Udonsharp-Skill` first. Check the indexed
commit against the revision being changed, then read the located source. The
following are useful entry points, not promises that a graph snapshot is current.

| File / entry point | Trace next |
|---|---|
| `VipWhitelistManager.Start` | Cache/array initialization, `lists[]` injection, URL requests, local gates, initial owner |
| `OnStringLoadSuccess`, `ParseRole` | URL-to-role matching, roster replacement, cache invalidation, `roleListVersion` |
| `IsAuthed`, `IsDj`, `IsSuperAdmin` | `EnsureAccessCached`, name normalization, role and manual-list membership |
| `OnRowToggled` | Direction-specific Add/Revoke checks, manual list mutation, local refresh |
| `DjAdd`, `DjRemove`, `SetDjSystemEnabledState` | DJ permission gates, ownership request, dirty flags |
| `TrySerializeManualChanges`, `OnOwnershipTransferred` | Owner check, clog/frame retry, serialization request |
| `OnDeserialization`, `NotifyLists` | Compaction, cache clearing, deferred rebuild versus toggle refresh |
| `VipWhitelistUI.Start`, `RetryStart` | Required references and bounded wait for manager injection |
| `RebuildPlayerList`, `AddRowIfMissing` | Present/offline rows, capacity, name keys and row pool |
| `SetRowInteractable`, `SetRowDjInteractable` | UI permission presentation; compare with manager mutation gates |
| `VipWhitelistRow.AuthToggled`, `DJToggled` | Serialized UI events, row state and forwarding |
| `Editor/VipWhitelistManagerEditor.cs` | Parallel role-array editing; update alongside added fields |

Preserve public event names and serialized fields used by prefabs. Keep the
manager Manual sync and UI/row NoVariableSync. Use fixed arrays and existing
cache invalidation paths; do not add a second independently synchronized role
authority. A hidden or disabled control is not the mutation permission check.

## Establish a usable development environment

There is no tracked `Packages/manifest.json`, `ProjectSettings`, `.csproj`,
`.asmdef`, package-manager manifest, standalone CLI test runner or CI workflow.
Do not run `dotnet test` or a made-up package build command against this root.

1. Select a separate compatible VRChat **Worlds** test project. Record its actual
   Unity, Worlds/Base SDK, UdonSharp, TextMeshPro, target platform and render
   pipeline versions before import. Preserve the creator's live scene/assets.
2. Check [VRChat's Unity requirement](https://creators.vrchat.com/sdk/upgrade/current-unity-version/)
   and [SDK release notes](https://creators.vrchat.com/releases/). Do not upgrade
   an existing project merely to perform a documentation or source review.
3. Import the three runtime source files and custom inspector into a dedicated
   folder. Keep NUnit editor tests in a test environment with the Unity Test
   Framework available; they are not runtime world components.
4. **Clean-import blocker:** this Git snapshot tracks no `.meta` files or Udon
   program assets. The prefabs and `Example Scene.unity` contain serialized GUID
   references whose original identities cannot be reconstructed from these files
   alone. Freshly generated metadata does not prove those references are fixed.
   Inspect every missing script/program/reference in an isolated import. Use an
   authoritative exported package retaining its metadata, or explicitly rebuild
   the wiring in the test scene. Do not fabricate replacement GUIDs.
5. Create one manager, one or more panels and an inactive row template. Add every
   panel to `manager.lists[]`; `manager` is injected into each panel at runtime.
   Panel `contentRoot` must have a RectTransform; `rowTemplate` must contain
   `VipWhitelistRow`, TMP name text and the intended Toggles.
6. Configure role arrays through the inspector, trusted text URLs, explicit
   Super Admin names and both barrier arrays as described in README/CONTRACT.
   Do not disable the manager or its own UI hierarchy as an access barrier.
7. Wire row Auth/DJ toggles to the row UdonBehaviour's `SendCustomEvent` with
   `AuthToggled` / `DJToggled`, and the panel's DJ-system toggle to
   `DJSystemEnabled`. Inspect serialized events; method names alone do not wire a
   prefab. The presence indicator should be read-only.
8. Compile C# and Udon in that project, inspect errors, and run the acceptance
   matrix below. No supported PC/Android/VR or clean-import claim follows from
   the reference scene or this documentation audit.

Local environment evidence only: the inspected Cliffside Venue metadata on
2026-10-31 records Unity **2022.3.22f1** and Worlds/Base **3.10.5**. Those versions
are not pinned by this repository, were not launched here, and are not a claim
of current compatibility or a minimum SDK version.

## State and operational behavior

Manual VIP and DJ grants and `syncedDjSystemEnabled` are instance-wide manager
state. `initialOwner` is a normalized display name recorded by the manager owner
when it can initialize it; it is not the changing network owner. A player's
departure does not intentionally revoke a manual entry. There is no cross-visit
PlayerData/PlayerObject persistence implementation or reset button in this asset.

Each client requests its configured role URLs at Start. The first matching URL
entry handles a response, so use distinct URLs per role. The parser accepts comma
and newline separators, removes CR, trims names, skips empty tokens and
case-insensitively deduplicates. It processes at most 1,000 tokens and stores at
most 256 distinct names per role. It is not a JSON/CSV-with-quoting parser: comment
lines and HTML error pages are not recognized as such. Serve plain-text rosters.

No role refresh timer or retry policy is implemented. A successful download
replaces that client's parsed roster; a failed download logs an error and does
not load replacement data. Different join times, failures and URL edits can
therefore produce different local access results. `roleListVersion` is a rebuild
signal, not a synchronized roster, snapshot hash or proof of equal permissions.

For URL troubleshooting, verify the response is raw text, the configured URL is
unique, the roster is within limits and each client can load it. VRChat's
[string-loading rules](https://creators.vrchat.com/worlds/udon/string-loading/)
include trusted-domain restrictions and a queued download rate. Do not solve a
loading failure by putting a secret token in a client-visible URL. Roster names,
URLs and verbose logs are not private storage.

Removing a manual VIP/DJ grant does not negate a role-based grant. The UI should
return to the effective access state; it is not a deny-list editor. DJ-system Off
opens DJ barriers for everyone while retaining the manual DJ list, and On
restores membership-based gating. Configure that switch accordingly.

## Known implementation and delivery gaps

These are source observations and release gates, not approved rule changes. They
do not authorize runtime fixes as part of this documentation PR.

| ID | Observation / requirement affected | Next evidence or action |
|---|---|---|
| UD-01 | Missing tracked Unity metadata/program assets; prefab clean import is unverified | Recover authoritative identities or explicitly rewire a separate scene; compile and inspect every reference |
| UD-02 | Edits mutate local synced arrays immediately after `SetOwner`; outgoing-owner updates can overwrite speculative edits | Preserve the existing owner-only publication rule; assign and test transfer/race handling separately |
| UD-03 | Dirty flags clear when serialization is requested; no `OnPostSerialization` success acknowledgment is implemented | Test clog, transfer and serialization failure; do not claim acknowledged or loss-free delivery |
| UD-04 | `IsTargetReadOnly` checks the first matching role; overlapping role order can affect protection | Test multi-role targets against the intended Read-Only contract before supporting such configurations |
| UD-05 | `SetRowDjInteractable` permits an initial-owner override before Read-Only checks, while `DjAdd`/`DjRemove` reject that target for non-Super-Admins | Test UI feedback and manager enforcement; align presentation in a separately assigned source fix |
| UD-06 | VIP UI locks every recognized Super Admin target, stronger than older text describing only the caller's own row | Do not use a disabled row as proof that every exposed mutation path has identical policy; retain intended protection and review explicitly |
| UD-07 | Initial-owner fallback uses the local manager owner when platform creator is unavailable; no initialization retry is performed by `OnOwnershipTransferred` | Test unavailable creator, startup timing, departure/rejoin and transfer; do not promise durable creator recovery |
| UD-08 | URL rosters are local, fetched once, with no application retry or automatic refresh | Define and approve any new refresh/failure policy before implementation; test mixed-success clients |
| UD-09 | Display-name trust and legacy Super Admin-prefix recognition are not authenticated entitlement verification | Use raw platform names in integrations; do not claim paid entitlement enforcement or anti-cheat protection |
| UD-10 | README commercial restrictions conflicted with the tracked MIT template, which still has placeholder ownership fields | Owner must settle distribution terms/attribution before release; this PR does not edit the license |
| UD-11 | Existing tests are configuration/helper harnesses, not proof of multiplayer synchronization, Udon compile or headset behavior | Run the acceptance matrix in recorded environments; retain historical results as historical |

## Acceptance and evidence ledger

For every runtime case record source commit, editor/SDK/platform versions,
fixture, identities/roles, steps, expected and actual result, logs and status.
Use test display names and redact production roster data. As of this audit all
runtime cases below are **NOT RUN**; source/document checks do not close them.

| Test | Fixture and observable result |
|---|---|
| AT-01 Import/compile | Separate Worlds project: scripts/programs resolve, C# and Udon compile, no missing prefab/scene references |
| AT-02 Wiring/start | Panel in `lists[]` receives manager; rows appear; missing RectTransform/template or absent manager is detected; test the bounded 30-frame manager wait |
| AT-03 Roles/URLs | Load raw mixed-case/comma/newline rosters, empty/duplicate/excess entries, failed URL and shared URL; compare each client's actual access and diagnostic output |
| AT-04 Permission direction | Add-only, Revoke-only, VIP-only, DJ-only, Super Admin and initial-owner fixtures: allowed action succeeds; denied action through UI **and direct manager method** leaves shared state unchanged |
| AT-05 Protected/overlap | Read-Only targets and overlapping roles in both orders preserve intended protection; Super Admin UI behavior is checked separately from manager mutations (UD-04/05/06) |
| AT-06 Effective gates | Each client sees its own VIP/DJ blockers; manual revoke does not remove a role grant; DJ Off opens barriers and On restores gating without losing the list |
| AT-07 Transfer/race | Two permitted editors, including a nonowner, edit concurrently and during ownership departure; record lost edits/divergence rather than assuming convergence (UD-02/03) |
| AT-08 Late join/lifetime | Third client reconstructs current manual state; leaving preserves intended entries; new instance starts fresh; owner departure/rejoin and unknown creator exercise UD-07 |
| AT-09 UI/capacity | Present and offline qualifying rows, 256-row limit, 128-player buffer, full manual arrays, rejected mutations and repeated toggles remain readable and truthful |
| AT-10 Platform/reset | Desktop and claimed VR/platform targets: interaction reach, input, layout and barriers work; disable/enable and scene reload do not fabricate persistence or recovery |

Source/document validation for the 2026-10-31 round: API/source tracing and
relative-link/path checks are performed separately from these runtime cases.
The PR description records the actual commands and results. No Unity launch,
package export, world upload, runtime certification or licensing decision is
part of this round.

## Change and handoff checklist

Before a change, state the affected contract rule, intended behavior and source
entry point. Preserve the creator checkout and `.meta` identities. Make an
isolated branch; update the contract, definitions, networking, setup and test
cases affected by that change together. Do not copy fixes from the separately
maintained `VIP-Manager` repository without an explicit integration decision.

Before delivery, review the actual diff, verify links and commands against tracked
files, run relevant source checks, then record which Unity/client/platform gates
were executed. Back up customized prefabs/scenes before authorized regeneration.
For removal, first remove references from panels, gates and calling scripts in a
test scene; then remove the asset folder with its matching metadata. Reference
documents/snippets can be removed independently. Preserve external roster data
and published world versions until their owners decide their lifecycle.

Unresolved human decisions: distribution/license reconciliation (UD-10), approved
target platforms and minimum supported environment, and any requested changes to
role refresh, identity or persistence policy. Existing contract rules remain the
starting point; the absence of runtime evidence is an engineering validation gap,
not permission to invent new product policy.
