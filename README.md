# VIP Manager

Simple inspector-based setup for the VIP Manager package.

## Overview
- Role-based VIP and manual whitelist system for VRChat worlds.

## Requirements
- Unity with VRChat SDK3
- TextMeshPro

## Quick start (inspector only)
1. Import the provided .unitypackage into your Unity project.
2. In your scene, add the "VIP Manager" prefab (or a GameObject with the VipWhitelistManager component).
3. Add one or more "VIP UI" prefabs (objects with the VipWhitelistUI component) to your scene.
4. In the VIP Manager inspector, open the "Lists" (or "List instances") field and add each UI instance you placed in your world.
5. Make sure the List instances each have the VIP Manager specified in them. Only one manager per project.
6. Configure roles, options, and visuals in the inspector.

## Set up objects and admins
- Objects To Disable When Authed: drag objects here that should be disabled locally when a player is authorized (for example, VIP colliders or signs).
- Super Admin Whitelist: enter exact player display names (one per entry). Players on this list always have full access.
  - Note: after changing this list you must reupload the world for the change to take effect.

## Debug
- Enable Debug Logs: toggle to show basic status messages.

## Roles
- Roles are listed in order. For each role set:
  - Priority: If a player is in multiple roles, the color and role name will be from the highest role they are a part of in the list. 
  - Name: visible role name shown in the UI.
  - Member list URL: a raw pastebin URL containing one name per line.
  - Color: UI color for the role.
  - Permissions: toggles for whether role members can add players, revoke players, have VIP access, or be read-only.

## UI row template
- Each VipWhitelistUI needs a row template prefab assigned to `rowTemplate` and a content `Transform` assigned to `contentRoot`.
- The included "VIP UI" prefabs already include a row template, so creating one is only necessary for custom UIs.

## Runtime notes
- Role member lists loaded from URLs are fetched automatically when the world starts.
- Manual changes made in the UI are synced to other players.
- Keep "Max Synced Manual" at a reasonable number for best performance.

Everything above is done via the Inspector. No editing of scripts is required.
