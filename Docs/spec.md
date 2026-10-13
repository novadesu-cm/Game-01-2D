# God of the Arena — Prototype Spec (for code)
Source: Prototype GDD v1.0. If this file conflicts with the GDD, the GDD wins. All numbers are "pending test" values stored in ScriptableObjects.

## 0. Prototype loop
Build select (1 Weapon + 1 Trait, free choice, no budget) → Wave 1 → Extract/Continue → Wave 2 → Extract/Continue → Wave 3 → Result → Play again.
One Fighter (Gladiator). No HP regen between waves; Wave 1 starts at MaxHP.
Show Best Pot (session) on Build and Result screens. Defeat loses the run Pot (not counted). Extract and Clear W3 count full Pot. Timeout counts floor(Pot × 0.5) (counts toward Best Pot).

## 1. Arena & physics (D-05 custom, deterministic)
- Simulation vectors use SimVec2 (float x, y), never UnityEngine.Vector2 (QA-07).
- Top-down 2D circle, center (0,0), ArenaRadius = 6.0. No walls.
- Ring-out: distance(pos,(0,0)) > 6.0 → dead (cause RingOut). Exactly 6.0 = alive. Unit radius is NOT used.
- Units: mass 1, drag 4. Each tick: vel *= 1/(1 + 4*0.02); pos += vel*0.02. Impulse: vel += dir*impulse. (Impulse 8 ≈ 2.0 u slide.)
- Unit-to-unit collision is REQUIRED: circle bodies (BodyRadius) push apart all living units (both teams), resolved in UnitID order, split 50/50.
- Dead unit: no collision, no AI, disappears within 1.0 s (visual only).
- All ranges measured center to center.

## 2. Stats
Gladiator: MaxHP 120, MoveSpeed 3.0, DmgMult 1.0, AtkSpdMult 1.0, KBMult 1.0, KBResist 0.10, BodyRadius 0.40

| Weapon | BaseDamage | AttackInterval | Windup | BaseKnockback | Range |
|---|---|---|---|---|---|
| Fists (default) | 8 | 1.0 | 0.20 | 3.0 | 1.1 |
| Giant Hammer | 24 | 2.0 | 0.60 | 9.0 | 1.5 |
| Dual Daggers | 5 | 0.4 | 0.08 | 1.0 | 1.0 |

| Trait | Effect |
|---|---|
| Lifesteal | on hit heal floor(ActualDamage × 0.25), max MaxHP |
| Thick Skin | damage taken × 0.75 (no knockback reduction) |

| Enemy | MaxHP | MoveSpeed | Damage | AttackInterval | Windup | Knockback | Range | KBResist | BodyRadius |
|---|---|---|---|---|---|---|---|---|---|
| Grunt | 40 | 2.4 | 4 | 1.2 | 0.30 | 2.5 | 1.1 | 0 | 0.40 |
| Brute | 100 | 1.5 | 9 | 2.0 | 0.70 | 7.0 | 1.4 | 0.40 | 0.55 |
Enemies: DmgMult = KBMult = AtkSpdMult = 1.0, no Trait.

EffInterval = AttackInterval / AtkSpdMult; EffWindup = Windup / AtkSpdMult.

## 3. Hit Resolution (exact order; A = attacker, D = defender)
1. x = A.BaseDamage × A.DmgMult
2. (Spear — not in prototype, keep hook)
3. (Berserker — not in prototype, keep hook)
4. If D has Thick Skin: x ×= 0.75
5. FinalDamage = max(1, RoundHalfUp(x))
6. ActualDamage = min(FinalDamage, D.HP); D.HP -= ActualDamage
7. If A has Lifesteal and A.HP > 0: A.HP = min(A.MaxHP, A.HP + floor(ActualDamage × 0.25))
8. Impulse = A.BaseKnockback × A.KBMult × (1 − D.KBResist). Dir = normalize(D.pos − A.pos); if dist < 0.001 use A facing (±x). Apply to D (visual only if D.HP = 0).
9. D.StaggerTimer = max(D.StaggerTimer, 0.1 + 0.04 × Impulse)
No other damage sources. One hit = one target.

### Unit tests (P-02)
1. Gladiator+Hammer hits Grunt → Final 24, Impulse 9.0, Stagger 0.46
2. Gladiator (HP 100/120)+Hammer+Lifesteal hits Grunt → Heal 6, HP 106
3. Gladiator+Daggers hits Grunt → Final 5, Impulse 1.0, Stagger 0.14
4. Grunt hits Gladiator+Thick Skin → Final 3, Impulse 2.25, Stagger 0.19
5. x = 0.4 after step 4 → Final 1 (EX-6)
6. Attacker with HP 0 gets no Lifesteal
7. dist < 0.001 → direction = attacker facing
Floats ±0.001; integers exact.

## 4. AI (all units; no Retreat)
States: Frozen, Chase, Ready, Windup, Dead, Idle.
- Chase: vel = normalize(Target.pos − pos) × MoveSpeed. dist ≤ Range → Windup if Cooldown ≤ 0 else Ready.
- Ready: vel = 0, face target. dist > Range → Chase; Cooldown ≤ 0 → Windup.
- Windup: stand, lock target, timer EffWindup, set Cooldown = EffInterval on enter. On end: if target alive and dist ≤ Range + 0.3 → hit resolves (tick step 3); else miss. If target dies during windup → cancel, Cooldown = 0, retarget next tick.
- Stagger: while StaggerTimer > 0 or |vel| > MoveSpeed, AI must not set vel and must not start Windup. Stagger does not cancel a started Windup. Cooldown always ticks.
- Facing: flip by target x each tick.
- Targeting: nearest living enemy. Retarget when none, target dead, or every 0.5 s (switch only if new one ≥ 0.5 u closer). Never during Windup. Tie (within 0.1 u): lower current HP, then lower UnitID.

## 5. Tick order (every 0.02 s, Combat only)
1. Ring-out check (mark dead, cause RingOut)
2. Timers −0.02: Cooldown, Stagger, Windup, Retarget, WaveTimer
3. Hit Resolution for all windups ending this tick, ascending attacker UnitID. Attacker killed earlier this tick still hits (no Lifesteal). Attacker ring-outed in step 1 does not hit.
4. Death marking (HP ≤ 0, record last attacker)
5. Outcome check (stop immediately if any)
6. AI update
7. Physics step (custom, includes collision)
Outcome order: 1) Defeat (no living fighter, even if enemies also all died) 2) Cleared 3) Timeout: WaveTimer reaches 60 s (D-02).
On outcome: all units Idle, vel = 0, physics off that tick. No damage/ring-out outside Combat.

## 6. Waves (UnitID: Fighter 1; enemies 101+ in listed order)
Fighter spawns at (−3.0, 0), vel 0, Stagger 0, Cooldown 0.
| Wave | Enemies & spawn | Reward | Pot total | Time |
|---|---|---|---|---|
| 1 | Grunt (3.0, 1.0), Grunt (3.0, −1.0) | +30 | 30 | 60 s |
| 2 | Grunt (2.5, 1.0), Brute (3.5, −1.0) | +50 | 80 | 60 s |
| 3 | Grunt (2.5, 1.5), Grunt (2.5, −1.5), Brute (3.5, 0.0) | +100 | 180 | 60 s |
D-03: each enemy spawn gets seeded offset ±0.3 u (System.Random(seed)); log seed every run.

## 7. Debug log (P-06) — CSV per run
seed, build, wave results, decision time per Extract/Continue, choice, cause of death (RingOut/HP + last attacker), knockback distances per weapon, run duration, Pot, Best Pot.
Batch mode: 20 seeds per build to confirm outcomes are not all identical.
- Knockback distance (D-08): measurement-only KbVel per hit; KbVel += dir*impulse, each tick KbVel *= 1/(1+4*0.02), add |KbVel|*0.02, for 50 ticks or until the target dies. KbVel never affects Pos/Vel.
- G1 = sanity check only; G1b = RingOut count per weapon per run (D-09).
- Decision time = from Extract/Continue overlay shown to button press (excludes transitions). Continue rate is computed from decision rows, not stored.

## 8. Task order
P-01a data+sim skeleton → P-02 HitResolver → P-01b movement/knockback/collision → P-01c ring-out+determinism+debug view → P-03 AI+tick → P-04 waves/Pot/Extract/Timeout → P-05 UI → P-06 log → P-07 playtest.
Determinism test: same seed run twice for 500 ticks → identical positions every tick.
