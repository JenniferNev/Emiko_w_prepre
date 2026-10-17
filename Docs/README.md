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
- **Phase:** 4 (UdonSharp). H, C, N and O prefabs bond with `ElementAtom.cs`: methane, ethane, water and ammonia work in ClientSim. Table originals are dispensers; click drops, hold deletes, right-click replaces an end atom.
- **Must work in VR** (headset and hand controllers), not only on desktop.
- **Next:** a first VR test (private upload), then grabbing a molecule by any atom. The full list is in [chemistry-world.md](chemistry-world.md#next-steps).
