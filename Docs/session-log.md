# Session log

What was done, in order. Commits are on `world/scisci` unless noted.

## 2026-10-10: Mirror world (`main`)
- **Mirror toggle button:** a Udon Graph program with Interact → `get activeSelf` → `op_UnaryNegation` → `SetActive`, wired to a public `VRCMirror` variable.
  - **Bug:** the button hid itself because the public variable was never assigned (unassigned falls back to `this`).
  - **Then:** made the button invisible by disabling only its Mesh Renderer.
- **Freeze diagnosed with gdb:** the UdonSharp `RuntimeLogWatcher` had re-registered its cleanup handler thousands of times because it couldn't find the VRChat data folder. Fixed with the Steam compatdata symlink.
- **Learning plans:**
  - World plan: see [learning-plan.md](learning-plan.md).
  - Avatar plan: `Avis/Projects/Avatar_Learning_Path.md`.
- **Git set up:** LFS and `.gitignore` (`a38897c`).

## 2026-10-11: Phase 1 and the chemistry branch
- **Spawn point**, ClientSim testing, private upload of the mirror world (`912a571`).
- **First project copy failed:** `Emiko_w_scisci` threw SDK errors because Unity's `Library/` cache was copied too.
- **Organization decided:** one repo with a branch per world, plus the naming convention (see [project-setup.md](project-setup.md)).
- **Branch created:** `world/scisci` (`b6c0610`), with the world ID cleared and the Phase 2 table files brought in.
- **NixOS (outside this repo):**
  - upgraded to 26.05
  - ALCOM 1.1.8 runs in Unity Hub's environment
  - ALCOM starts on workspace 2
  - Steam compat tools fixed

## 2026-10-12: Table
- **Table:** lightmap UVs enabled, source renamed to `LabTable.blend` (`1e3ceab`).
- **Group:** everything in the lab goes under `ChemistryLab`, scaled as one (`c7b7887`).

## 2026-10-13: Element data and shaders (`5eb504e`)
- **Atom shaders:** the unlit sphere shader and the billboarded symbol shader.
- **`elements.json`:** all 118 elements, from Jmol, Cordero, Pyykkö, PubChem and NIST. It's built by `Tools/elements/build_elements.py`.
  - **Checked:** the radii match the published tables, and NIST bond lengths confirm the "bonded atoms touch at r₁ + r₂" sizing.

## 2026-10-14: Hydrogen works
- **Symbol material:** made from the TMP Outline material, because the font's default material is hidden inside the font asset.
- **Fix:** the letter tilted with the atom and sometimes vanished. Cause: dynamic batching. Fixed with the `DisableBatching` tag (`e112a25`).
- **Fix:** half the letter was cut off at some angles. Cause: the letter copied the screen's rotation, so for off-center atoms it tilted into the sphere. It now faces the viewer's eye position (`3663972`).

## 2026-10-14/15: Bonding
- **`ElementAtom.cs`** (UdonSharp): bond sockets built from measured angles, snap-on-release at the real bond length, molecules parented to their anchor atom (`0a097cc`).
  - Socket angles checked numerically.
- **Next:** program asset, Carbon prefab variant, methane test.

## 2026-10-15: Methane works
- **Program asset:** the first `ElementAtom.asset` was an Udon *Graph* asset (made with Udon Behaviour → New Program). Replaced it with an Udon C# Program Asset in `ChemistryLab/Scripts/` and added Element Atom directly to `Atom_H`.
  - The "Source C# script is null" errors came from UdonSharp compiling the new asset before its script was assigned.
- **Prefabs:** `Atom_H` and its variant `Atom_C` (0.152, `#909090`, C, 4 bonds, tetrahedral 109.471°).
  - The Carbon values were first set as scene overrides, and Max Bonds was left at 1. Fixed with Overrides → Apply All.
- **Fix:** element letters were mirrored left-to-right (only visible on C). Cause: a cross product in the wrong order for Unity's left-handed world (`ed8811b`).
- **Test:** four H's snap onto the C at 10.7 cm in tetrahedral directions: CH₄ ✅
- **Next:** dispenser, then O and N.

## 2026-10-16: Dispensers, controls, replace (`fc9dc95`)
- **Dispenser:** table originals (`isDispenser`) leave an `Instantiate` copy at their saved home spot when grabbed. Originals never bond.
- **Controls** (Jenn's design): click Use = drop and bond, hold Use 0.6 s = delete (via `SendCustomEventDelayedSeconds`), VRChat's drop (right click) = replace. Pickup Auto Hold set to Yes, so ClientSim sends Use events.
- **Replace:** a free atom dropped on an end atom takes its place. C on an H of methane gives H₃C–C, and three more H's make ethane ✅
- **Fix:** the first delete version threw `NullReferenceException`s: it destroyed the atom inside `OnPickupUseDown`, and ClientSim kept moving it. Delete now runs from the delayed event, after `Drop()`.
- **ClientSim quirk:** keeping the grab button down sends a Use at 0.5 s; a UseDown within 0.6 s of the grab is ignored.
- **Next:** O and N, then Clear Chemicals.
