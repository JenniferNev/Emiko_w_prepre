# Project docs: Emiko_w_prepre

Context saved from the build sessions with Claude, so work can resume from these files.

| File | What's in it |
|---|---|
| [learning-plan.md](learning-plan.md) | VRChat world-building study plan (phases 0–7), progress, tools |
| [chemistry-world.md](chemistry-world.md) | The chemistry lab world: goal, design decisions, element data, shaders, bonding |
| [project-setup.md](project-setup.md) | Git branches, naming convention, Linux/NixOS setup, problems solved |
| [session-log.md](session-log.md) | What was done, in order, with commit IDs |

## Current state (2026-10-15)

- **Branch:** `world/scisci`, the chemistry lab world (no world ID yet, so the first upload creates a new world).
- **`main`:** the original mirror world (`wrld_aefe175a-146d-4551-9c9d-1b3e7dca4f2b`). Never merge `world/scisci` into it.
- **Phase:** 4 (UdonSharp). `Atom_H` and `Atom_C` prefabs bond with `ElementAtom.cs`: **methane (CH₄) works** in ClientSim.
- **Next:** a dispenser (grab a table atom, a copy spawns in its place), then oxygen and nitrogen (water, ammonia).
