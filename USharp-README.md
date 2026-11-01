# UdonSharp reference library

This is the reference-document and snippet surface of the repository. The root
also contains the legacy VIP Manager asset, documented in [README.md](README.md)
and [MAINTENANCE.md](MAINTENANCE.md). Reusing learning material does not require
copying those runtime scripts or prefabs into another world.

## Actual contents and authority

- `.github/copilot-instructions.md`: repository instructions with general
  UdonSharp notes. Its location is relative to this repository's root.
- `docs/USharp-*.md`: 25 reference documents, including networking-events and
  networking-specs supplements. These are reference snapshots, not a versioned
  SDK specification or approved requirements for every importing project.
- `snippets/USharp-udonsharp.code-snippets`: an export wrapper containing 41 C#
  entries under `snippets[]`, plus `version`, `scopedPackageInstallation` and
  `language` metadata. It is **not** VS Code's native named-snippet object despite
  its extension. No `snippets/udonsharp.code-snippets` is supplied.
- `AGENTS.md`: current agent entry point and exact managed Graphify instructions.

There is **no tracked `CLAUDE.md`, `SKILL.md`, plugin manifest, agent installer,
package manifest or automatic registration script**. Do not claim this checkout
is installed as a named skill, or that files copied beneath Unity Assets are
automatically discovered by any coding assistant.

The importing project's own contracts and agent instructions remain authoritative.
Check API and language availability against its installed Worlds SDK/UdonSharp
and current [VRChat Creator documentation](https://creators.vrchat.com/worlds/udon/).
Do not copy an illustrative networking, moderation, persistence or permission
example as a product decision.

## Find the right reference

All paths below are relative to this root, with the `USharp-` prefix retained.

| Topic | Tracked reference |
|---|---|
| Language and API | [Overview](docs/USharp-01-udonsharp-overview.md), [VRChat API](docs/USharp-02-vrchat-api.md) |
| Players | [Player API](docs/USharp-03-players.md) |
| Networking | [Ownership/sync](docs/USharp-04-networking.md), [events](docs/USharp-04b-networking-events.md), [specs](docs/USharp-04c-networking-specs.md) |
| Data and persistence | [Containers](docs/USharp-05-data-containers.md), [persistence](docs/USharp-06-persistence.md) |
| Input and UI | [Input events](docs/USharp-07-input-events.md), [UI events](docs/USharp-08-ui-events.md) |
| Editor and debugging | [Editor scripting](docs/USharp-09-editor-scripting.md), [debugging](docs/USharp-10-debugging.md), [debug views](docs/USharp-13-world-debug-views.md) |
| Remote data and graphics | [External URLs](docs/USharp-11-external-urls.md), [VRCGraphics](docs/USharp-12-vrc-graphics.md) |
| Build and low-level behavior | [Build/test](docs/USharp-14-build-test.md), [VM/assembly](docs/USharp-15-vm-assembly.md), [event order](docs/USharp-22-event-execution-order.md) |
| World assembly | [Layers](docs/USharp-16-layers.md), [allowed components](docs/USharp-17-allowlisted-components.md), [scene components](docs/USharp-18-scene-components.md), [world setup](docs/USharp-19-world-setup.md) |
| Other systems | [Video](docs/USharp-20-video-players.md), [MIDI](docs/USharp-21-midi.md), [items](docs/USharp-23-items.md) |

## Reuse deliberately

1. Read the destination project's instructions and choose only the relevant
   references. Put them in a documentation location that project actually uses,
   retaining their provenance and links. Do not import duplicate VIP runtime
   types into a world just to make notes available.
2. Point the assistant at those documents explicitly. If adopting Copilot
   instructions, merge relevant guidance into the destination's supported
   instruction location without overwriting its existing policy. Check that
   client's current discovery behavior instead of assuming multi-root or nested
   files are automatically merged.
3. For VS Code workspace snippets, **convert the selected `snippets[]` entries**
   into VS Code's native top-level object keyed by unique descriptive snippet
   names. Each value should copy that entry's `prefix`, `body` and `description`
   and add `"scope": "csharp"`. Omit the export-wrapper metadata and the
   `snippets` array. Save the converted object as a `.code-snippets` file beneath
   the destination workspace's `.vscode` folder, preserving existing snippets.
   Copying the tracked wrapper unchanged does not install those prefixes. Follow
   [VS Code's snippet format](https://code.visualstudio.com/docs/editing/userdefinedsnippets).
   Review the generated code first; do not overwrite user-wide files/settings.
4. In a C# file, request the desired prefix from completion. Examples include
   `udon-behaviour`, `udon-behaviour-manual`, `udon-behaviour-continuous`,
   `udon-interact`, `udon-input-use`, `udon-get-players`, `udon-string-load`,
   `udon-network-callable-params`, `udon-ownership-request`, and
   `udon-get-component-udon`. Inspect the JSON for the complete prefix list.
5. Compile the resulting script with the destination project's installed
   UdonSharp, then test its intended behavior. Expanding a snippet or parsing C#
   does not prove Udon API exposure, network correctness or VR usability.

No install, hook registration, global configuration write, semantic extraction,
Unity launch or dependency upgrade is required merely to read these references.
If a client needs a packaged skill or MCP server, treat that as a separate setup
task with its actual manifest and connection requirements.

## Maintain and validate references

When updating a topic, record the official source URL, checked date and relevant
installed SDK/version. Correct the owning topic and affected snippets together;
keep platform facts separate from product choices. Follow the local-only versus
shared-state and ownership distinctions in the consuming project's contract.

Check relative links against the real tracked names and review code examples
against the installed API exposure tree. Snippets have placeholders and are
templates, not an automated test suite. This documentation round checked
inventory, links and source alignment only; no claim that every reference
example compiles or that all 41 snippets were exercised is made.

The legacy VIP asset's current import/runtime gaps, license conflict and
acceptance tests are in [MAINTENANCE.md](MAINTENANCE.md). Its historical test
report is not evidence for another project using these notes.
