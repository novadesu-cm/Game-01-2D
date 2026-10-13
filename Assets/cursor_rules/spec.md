---
description: God of the Arena Prototype rules
alwaysApply: true
---
# God of the Arena — Prototype rules
- Engine: Unity 2D, C#. Do NOT add packages.
- Source of truth: Docs/spec.md (from Prototype GDD v1.0). If unsure, ask; never invent rules.
- Do NOT use Rigidbody2D, Collider2D or Unity physics for gameplay (D-05). Custom deterministic physics only.
- Fixed dt = 0.02 s. Never use Time.deltaTime or UnityEngine.Random inside simulation. Randomness = System.Random(seed) only.
- All stats live in ScriptableObjects (D-04). No hard-coded tuning numbers in logic.
- Rounding: Math.Round(x, MidpointRounding.AwayFromZero). Never Mathf.Round.
- Do ONE task per request. Do not modify files outside the task scope. Do not refactor unrelated code.
- Every task ships EditMode tests (Unity Test Framework). Float comparisons use tolerance ±0.001; integers compare exactly.
- Out of scope (do not build): Hub, economy, shop, save/load, Recruit, Tank, Spear, Retreat, Berserker, pause, host text, art/audio beyond placeholders, multiplayer.
- When finished: list files created/changed and how to run the tests.

## Architecture (the bridge)
- ArenaSim and everything in Assets/Scripts/Simulation is pure C# and must NOT reference UnityEngine (no Vector2, Mathf, Debug, ScriptableObject, MonoBehaviour).
- Vectors in the simulation use our own struct SimVec2 (float x, y) with the math helpers it needs. Convert to UnityEngine.Vector2 only inside ArenaView.
- Use System.Math / MathF, never Mathf, inside Simulation.
- Data passing: ScriptableObjects (Unity side) are converted into pure C# config classes/structs (DTOs) at initialization and passed into ArenaSim.
- Rendering: MonoBehaviours read ArenaSim state by polling in Update() or by subscribing to pure C# Action events. No visual logic inside ArenaSim.
- Enforcement: Assets/Scripts/Simulation has an Assembly Definition `GotA.Simulation` with "No Engine References" checked. Assets/Scripts/Unity has `GotA.Unity` referencing GotA.Simulation. Assets/Tests/EditMode has a test asmdef referencing GotA.Simulation (and GotA.Unity only when a test truly needs ScriptableObjects).

## Folder structure
- Assets/Scripts/Simulation  -> pure C# (ArenaSim, SimUnit, SimVec2, HitResolver, config DTOs)
- Assets/Scripts/Unity       -> MonoBehaviours (ArenaView, UI), ScriptableObject definitions, SO-to-DTO converters
- Assets/Data                -> ScriptableObject assets
- Assets/Tests/EditMode      -> EditMode tests
