# VRChat world-building learning plan

**Goal:** understand the fundamentals at a basic level, then build worlds for avatars and games. The capstone is the chemistry lab world ([chemistry-world.md](chemistry-world.md)).

**Method:** every phase ends with something built in this project. Pair one Blender skill with each Unity phase.

## Phases

| # | Phase | Learn | Build | Status |
|---|---|---|---|---|
| 0 | Setup | VCC/ALCOM, SDK, scenes, Udon Graph, public variables | Mirror toggle button | ✅ Done |
| 1 | World basics | Spawn points, respawn height, ClientSim testing, private upload | Spawn at (0, 0, 10), private upload | ✅ Done (2026-10-11) |
| 2 | Blender → Unity | Modeling, apply transforms, UVs, materials, FBX export/import, colliders | Lab table, atom sphere | 🟡 Table in, inside the `ChemistryLab` group |
| 3 | Lighting & performance | Baked vs real-time, Static flags, light/reflection probes, occlusion, Quest limits | Baked-light lab room | ⬜ |
| 4 | UdonSharp | Events (Interact, OnPickup, OnDrop…), arrays, SendCustomEvent | Grabbable atoms that bond | 🟡 H and C prefabs bond; methane works (2026-10-15) |
| 5 | Networking | Ownership, `[UdonSynced]`, RequestSerialization, late joiners, Object Pool/Sync | Everyone sees the same atoms and bonds | ⬜ |
| 6 | UI & persistence | Canvas + VRC UI Shape, TMP input, PlayerData | Name and save compounds | ⬜ |
| 7 | Polish | Shaders, audio, AudioLink, video players, publishing | Make it look and sound good | ⬜ |

### Phase 7 extra: writing shaders (added 2026-10-14)

ShaderLab/HLSL in the built-in pipeline:
- unlit vs lit
- vertex/fragment programs
- properties
- render queues
- depth (ZTest/ZWrite)
- billboarding
- VR stereo/instancing macros
- SDF text

Study material: this project's own `Assets/ChemistryLab/Shaders/AtomUnlit.shader` and `AtomSymbol.shader`. Read them line by line, then write a small shader yourself.

## Phase 1 notes (world basics)
- **Spawn point:** an empty `Spawn` object, dragged into VRCWorld → Scene Descriptor → Spawns.
- **Testing:** use **ClientSim** (press Play in Unity). Skip **Build & Test**: it launches the Windows VRChat client and doesn't work on Linux.
- **Uploading:** VRChat SDK → Control Panel → Builder. Set visibility **Private** and build for the Windows platform. Your account needs the New User trust rank.
- **After the first upload**, save the scene and commit right away: the world ID is stored in the scene (VRCWorld → Pipeline Manager).

## Phase 2 notes (Blender → Unity)
- **Files:** `.blend` sources go in `Blender/` (outside `Assets/`). FBX exports go in `Assets/Models/`.
- **FBX export settings:** Limit to Selected; Mesh only; Apply Scalings **FBX All**; Forward **−Z**; Up **Y**; Apply Unit ✓; Use Space Transform ✓; Apply Transform ✓; Apply Modifiers ✓; Smoothing **Face**. Save these as a "Unity" preset.
- **Check after import:** Rotation should read (0, 0, 0) and Scale (1, 1, 1). Anything like −90 or 100 means an export setting was off.
- **Re-exporting:** overwrite the same FBX file. Unity keeps its ID, so everything using it updates.
- **Import settings:** enable **Generate Lightmap UVs** on static models (needed in Phase 3).
- **Scaling:** use a uniform scale (the same value on X, Y and Z) on parent groups. Non-uniform scale on a parent skews rotated children.
- Blender is 5.x on this machine. The steps above were written for 4.4, so menus can differ slightly.

## Tools

**For worlds:**
- **ALCOM** (Linux-friendly VCC): packages, and opening projects
- **UdonSharp + ClientSim:** already in the SDK
- **VRWorld Toolkit:** checks world setup and performance
- **AudioLink, ProTV:** music-reactive effects and video
- **World shaders:** Mochie, Silent's Filamented
- **Lightmapping:** Bakery is likely Windows-only; use Unity's Progressive lightmapper instead.

**Avatar-only** (separate plan in `Avis/Projects/Avatar_Learning_Path.md`): Poiyomi, lilToon, VRCFury, Modular Avatar, Gesture Manager, SPS.
