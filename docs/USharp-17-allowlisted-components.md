# Allowlisted World Components

**Source:** https://creators.vrchat.com/worlds/whitelisted-world-components

---

## Overview

Only components on this list will function in VRChat worlds. Any component **not** listed here will be stripped or ignored.

> **Quest/Android:** Additional exceptions apply. Check [Quest content limitations](https://creators.vrchat.com/platforms/android/quest-content-limitations#components).

> **Shape limits:** `VRCContactReceiver`, `VRCContactSender`, `VRCPhysBone`, `VRCPhysBoneCollider` are subject to per-world shape count limits.

---

## Unity Components

AimConstraint, Animator, AudioChorusFilter, AudioDistortionFilter, AudioEchoFilter, AudioHighPassFilter, AudioLowPassFilter, AudioReverbFilter, AudioReverbZone, AudioSource, BillboardRenderer, BoxCollider, Camera, Canvas, CanvasGroup, CanvasRenderer, CapsuleCollider, CharacterJoint, Cloth, ConfigurableJoint, ConstantForce, EllipsoidParticleEmitter, FixedJoint, FlareLayer, Grid, Halo, HingeJoint, LODGroup, LensFlare, Light, LightProbeGroup, LightProbeProxyVolume, LineRenderer, LookAtConstraint, MeshCollider, MeshFilter, MeshParticleEmitter, MeshRenderer, NavMeshAgent, NavMeshObstacle, OcclusionArea, OcclusionPortal, OffMeshLink, ParentConstraint, ParticleAnimator, ParticleEmitter, ParticleRenderer, ParticleSystem, ParticleSystemForceField, ParticleSystemRenderer, PlayableDirector, PositionConstraint, Projector, RectTransform, ReflectionProbe, Rendering.SortingGroup, Rigidbody, RotationConstraint, ScaleConstraint, SkinnedMeshRenderer, Skybox, SphereCollider, SpringJoint, SpriteMask, SpriteRenderer, Terrain, TerrainCollider, TextMesh, Tilemap, TilemapRenderer, TrailRenderer, Transform, Tree, VideoPlayer, WheelCollider, WindZone, WorldParticleCollider

---

## VRChat Components

| Component | Notes |
|---|---|
| `VRC_AvatarPedestal` | For displaying/switching avatars |
| `VRCContactReceiver` | PhysBone contact receiver *(shape limits apply)* |
| `VRCContactSender` | PhysBone contact sender *(shape limits apply)* |
| `VRC_IKFollower` | **Deprecated** — use VRChat Constraints or Unity Constraints instead |
| `VRC_MidiListener` | Realtime MIDI input |
| `VRC_MirrorReflection` | In-world mirrors |
| `VRCPhysBone` | *(shape limits apply)* |
| `VRCPhysBoneCollider` | *(shape limits apply)* |
| `VRCPhysBoneRoot` | |
| `VRCPipelineManager` | World/avatar pipeline ID tracking |
| `VRC_PortalMarker` | World portals |
| `VRC_SceneDescriptor` | Required root component for every VRChat world |
| `VRC_SpatialAudioSource` | 3D spatialised audio |
| `VRC_Station` | Seats / stations |
| `VRC_UiShape` | Makes a Canvas interactable via VRChat UI pointer |

---

## Dynamic Bone *(Deprecated)*

- `DynamicBone` — **Deprecated.** Use `VRCPhysBone` instead.
- `DynamicBoneCollider` — **Deprecated.** Use `VRCPhysBoneCollider` instead.

---

## Text Mesh Pro

`TMP_Dropdown`, `TMP_InputField`, `TMP_ScrollbarEventHandler`, `TMP_SelectionCaret`, `TMP_SpriteAnimator`, `TMP_SubMesh`, `TMP_SubMeshUI`, `TMP_Text`, `TextContainer`, `TextMeshPro`, `TextMeshProUGUI`

---

## Unity Event System

`BaseInput`, `BaseInputModule`, `BaseRaycaster`, `EventSystem`, `EventTrigger`, `PhysicsRaycaster`, `PointerInputModule`, `StandaloneInputModule`, `TouchInputModule`, `UIBehaviour`

---

## Unity UI

`AspectRatioFitter`, `BaseMeshEffect`, `Button`, `CanvasScaler`, `ContentSizeFitter`, `Dropdown`, `Graphic`, `GraphicRaycaster`, `GridLayoutGroup`, `HorizontalLayoutGroup`, `HorizontalOrVerticalLayoutGroup`, `Image`, `InputField`, `LayoutElement`, `LayoutGroup`, `Mask`, `MaskableGraphic`, `Outline`, `PositionAsUV1`, `RawImage`, `RectMask2D`, `ScrollRect`, `Scrollbar`, `Selectable`, `Shadow`, `Slider`, `Text`, `Toggle`, `ToggleGroup`, `VerticalLayoutGroup`

---

## Post Processing Stack V2

> PPSv1 is **not** supported. Use PPSv2 only.

`PostProcessDebug`, `PostProcessLayer`, `PostProcessVolume`

---

## AVPro

`ApplyToMaterial`, `ApplyToMesh`, `AudioOutput`, `DisplayIMGUI`, `DisplayUGUI`, `MediaPlayer`, `SubtitlesUGUI`

---

## Oculus Spatializer Unity

`ONSPAmbisonicsNative`, `ONSPAudioSource`, `ONSPReflectionZone`, `OculusSpatializerUnity`

---

## Final IK

> VRChat's FinalIK implementation is **heavily modified** and may not match upstream documentation.  
> Custom FinalIK in worlds should work, but is not officially tested or supported.

`AimIK`, `AimPoser`, `Amplifier`, `AnimationBlocker`, `BehaviourBase`, `BehaviourFall`, `BehaviourPuppet`, `BipedIK`, `BipedRagdollCreator`, `BodyTilt`, `CCDIK`, `FABRIK`, `FABRIKRoot`, `FBBIKArmBending`, `FBBIKHeadEffector`, `FingerRig`, `FullBodyBipedIK`, `GenericPoser`, `Grounder`, `GrounderBipedIK`, `GrounderFBBIK`, `GrounderIK`, `GrounderQuadruped`, `GrounderVRIK`, `HandPoser`, `HitReaction`, `HitReactionVRIK`, `IK`, `IKExecutionOrder`, `Inertia`, `InteractionObject`, `InteractionSystem`, `InteractionTarget`, `InteractionTrigger`, `JointBreakBroadcaster`, `LegIK`, `LimbIK`, `LookAtIK`, `MuscleCollisionBroadcaster`, `OffsetModifier`, `OffsetModifierVRIK`, `OffsetPose`, `Poser`, `PressureSensor`, `Prop`, `PropRoot`, `PuppetMaster`, `PuppetMasterSettings`, `RagdollCreator`, `RagdollEditor`, `RagdollUtility`, `Recoil`, `RotationLimit`, `RotationLimitAngle`, `RotationLimitHinge`, `RotationLimitPolygonal`, `RotationLimitSpline`, `ShoulderRotator`, `SolverManager`, `TriggerEventBroadcaster`, `TrigonometricIK`, `TwistRelaxer`, `VRIK`
