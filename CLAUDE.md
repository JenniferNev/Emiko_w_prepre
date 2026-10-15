# Instructions for Claude: Emiko_w_prepre (VRChat worlds)

## Start of a session
1. Read `Docs/README.md` (current state and next step), then the doc for the topic at hand:
   - `Docs/chemistry-world.md`: chemistry lab design, element data, shaders, bonding
   - `Docs/learning-plan.md`: study phases and progress
   - `Docs/project-setup.md`: branches, naming, Linux fixes
2. Check `git branch --show-current`, `git status` and `git log --oneline -10`.
   - The chemistry lab is on `world/scisci`.
   - `main` is the mirror world.
   - Never merge between world branches: each branch holds its own VRChat world ID in the scene.

## Keep Docs/ up to date
After every **big milestone**, update `Docs/` before ending the turn. A big milestone is:
- a learning-plan phase or sub-goal finished
- a new element, prefab or mechanic working (e.g. a new atom, bonding, unbonding, dispensers, sync)
- a design decision made or changed
- a non-obvious problem solved (cause + fix)
- an upload, or a new branch or world

What to update:
- `Docs/session-log.md`: add a dated entry with what was done and the commit IDs.
- `Docs/README.md`: update "Current state" and "Next".
- The topic doc (`chemistry-world.md`, `learning-plan.md` status column, or `project-setup.md`) when its facts changed.

Then commit the Docs change (see Git below). Small fixes and questions don't need a Docs update.

## Git
- **Commit only your own files**, with a pathspec: `git commit -m "…" -- <paths>`. Jenn often has files staged, and a bare `git commit` would sweep them into your commit.
- Clear messages: a short subject, then the why.
- **Don't edit the `.unity` scene or prefabs on disk while Unity is open:** it can discard unsaved editor work. Shaders, scripts, data and `.meta` import settings are safe to edit.

## Teaching style
- Jenn is learning. Explain step by step and ground every explanation in the real project files.
- Jenn does the Unity clicks herself. Claude writes scripts, shaders, data and tools, verifies them (numbers, compile errors in `~/.config/unity3d/Editor.log`), and commits.
- Scientific data must come from cited sources (see `Docs/chemistry-world.md`), never from memory alone.
