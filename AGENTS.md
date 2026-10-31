<!-- graphify-rules-start (managed by `graphify init`) -->
## Use Graphify before grep

This repository is indexed by Graphify: a code graph over its call, dependency, and test structure, exposed through a connected Graphify MCP server. Before reaching for grep or reading files, use the Graphify tools your MCP client lists (their exact names and descriptions are in the server's tool list) for what the graph knows and a text search does not:

- find where a symbol, function, or class is defined (instead of grepping for it)
- understand how something works, or where a behavior is handled
- find who calls a function, or what it calls
- see what a change affects (its blast radius) and which tests cover it
- map a file's dependencies and dependents

Fall back to grep or file reads only for what the graph does not model: literal string or comment matches, non-indexed files, or reading a file you have already located. If no Graphify tools are listed, check the MCP server connection.
<!-- graphify-rules-end -->

## Project authority and scope

This repository combines the legacy VIP Manager asset with UdonSharp reference
documents/snippets. Read README, CONTRACT, DEFINITIONS, NETWORKING and MAINTENANCE
before changing the asset. CONTRACT defines intended behavior; MAINTENANCE records
source conformance gaps and verification status. Generic references under docs/
are learning material, not new product policy. USharp-README describes their real
paths and reuse. No standalone Unity project or installable agent-skill manifest
is tracked.

Use isolated source checkouts and preserve creator work, serialized names and
Unity metadata. This snapshot lacks tracked .meta/program assets; do not claim
prefab clean-import acceptance or fabricate GUIDs. Runtime fixes, source changes,
Unity launch, dependency upgrades, prefab generation and world publication are
separate from a documentation assignment. Keep observed source behavior separate
from approved rules; do not close gaps by weakening the contract. Do not transfer
requirements or implementations from the distinct VIP-Manager repository without
an assigned integration change.

Use meaningful validation appropriate to the change. Link/path/source checks are
not Unity, Udon, multi-client or headset acceptance. Preserve historical reports
as historical; record actual versions, commands, results and remaining NOT RUN
cases. Never add real roster identities, secret URL tokens or credentials to
documentation or examples.
