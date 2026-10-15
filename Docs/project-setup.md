# Project setup, conventions and problems solved

## Project basics

| | |
|---|---|
| Unity | 2022.3.22f1 |
| VRChat Worlds SDK | 3.10.5 (in `Packages/vpm-manifest.json`) |
| Opening the project | **ALCOM**. It runs inside Unity Hub's environment, so "Open Unity" works on NixOS. |
| Git | Git LFS for binary files. The VRChat SDK packages aren't committed; ALCOM restores them with **Resolve**. |
| Remote | `https://github.com/JenniferNev/Emiko_w_prepre.git` |

## Branches: one repo, one branch per world

| Branch | World | VRChat world ID |
|---|---|---|
| `main` | Mirror world (Phases 0–1) | `wrld_aefe175a-146d-4551-9c9d-1b3e7dca4f2b` |
| `world/scisci` | Chemistry lab | not uploaded yet |

The rules:
- **The world ID lives in the scene** (VRCWorld → Pipeline Manager → `blueprintId`), so each branch carries its own.
- **Never merge between world branches.** A merge would carry over the other branch's world ID.
- **Close Unity before switching branches** (`git switch …`), or at least save everything first.
- **After a new world's first upload**, save the scene and commit right away. Otherwise the next upload creates yet another new world.
- **Check a branch's world ID without switching to it:**

  ```
  git grep -h blueprintId <branch> -- '*.unity'
  ```

Planned naming for branches and tags:
- short work branches: `scisci/<topic>`
- tags: `scisci/phase-N`, `scisci/upload-N`

## Naming convention for published products

| Kind | Pattern | Example |
|---|---|---|
| Product | `<nickname>_<type>_<code>`, all lowercase | `emiko_wrld_scisci` |
| Version | `<product>.<subname>` | `emiko_wrld_scisci.<subname>` |
| Compound (several products) | joined with `-` | `jenn_wrld_prepre-avtr_ninani` |

- **Types:** `wrld` (world), `avtr` (avatar).
- **Codes:** 5–15 lowercase letters or digits, easy to repeat (`prepre`, `scisci`).
- **Development names** (branches, folders, scripts) don't need to follow this pattern. Keep them short and descriptive instead.

## Committing

- Commit `Assets/SerializedUdonPrograms/*` whenever Unity regenerates them. They change on every compile, so they keep coming back.
- `.blend1` backup files are gitignored.
- So is `Tools/elements/.cache/`, the downloaded source data for the element-data tool.

## Linux / NixOS fixes

**UdonSharp domain-reload freeze**
- **Symptom:** Unity froze on "Reloading Domain".
- **Cause:** a bug in UdonSharp's log watcher. It looks for VRChat's data folder at `~/.local/share/Steam/steamapps/compatdata/438100`, but VRChat lives in another Steam library. The watcher then re-registers a handler every editor frame, and the reload hangs for minutes.
- **Fix:** a symlink from the expected path to `/run/media/emikojenn/Linux/SteamLibrary/steamapps/compatdata/438100`.

**"Source C# script … is null" errors**
- **Cause:** leftover Udon C# Program Assets with no script assigned. The fix was to delete them.

**Copying the project caused errors**
- **Cause:** the copy included `Library/`, Unity's cache from the original.
- **Fix:** when copying a project manually, leave out `Library/`, `Temp/`, `Logs/` and `UserSettings/`. Give the copy its own world ID (Pipeline Manager → **Detach**) and add it to ALCOM.

**ALCOM couldn't copy projects ("Too many open files")**
- **Fix:** the NixOS config now starts ALCOM 1.1.8 inside Unity Hub's environment with a raised open-files limit (`/etc/nixos/pkgs/alcom-fhs`). ALCOM also starts on Sway workspace 2.

**Udon exceptions in the Console mentioning `GetPlayerTag` or `IsOwner`**
- These come from the **VRChat client's** log, from other worlds you visited. Not this project.

**Build & Test doesn't work on Linux**
- Test with ClientSim, or upload privately and visit the world.

## Unity gotchas learned
- **Udon Graph:** an unassigned public GameObject variable falls back to `this` (the object itself). The mirror button hid itself until `VRCMirror` was assigned.
- **TMP Essential Resources** must be imported once (Window → TextMeshPro).
- **TMP materials:** the font's default material is hidden inside the font asset. Duplicate `LiberationSans SDF - Outline` instead. Custom TMP materials must keep a name starting with `LiberationSans SDF` to show up in Material Preset.
- **Invisible buttons:** disable the **Mesh Renderer** only, not the GameObject, or the collider and Udon stop too.
