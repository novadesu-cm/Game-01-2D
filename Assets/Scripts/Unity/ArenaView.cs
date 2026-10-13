using System.Collections.Generic;
using GotA.Simulation;
using UnityEngine;

namespace GotA.Client
{
    /// <summary>
    /// Placeholder visual for the simulation: arena disc, Gladiator and enemies drawn with Unity primitives.
    /// READ-ONLY view: it polls GameSessionManager.ActiveSim every frame and only copies positions,
    /// facing, size and alive state onto GameObjects. No gameplay logic, nothing is written to the sim.
    /// Sim (x, y) maps to world (x, y, 0); look at it with a 2D camera (z = -10).
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        [SerializeField] private GameSessionManager session;

        [Header("Look")]
        [SerializeField] private Color arenaColor = new Color(0.20f, 0.20f, 0.24f);
        [SerializeField] private Color gladiatorColor = new Color(0.25f, 0.55f, 1.00f);
        [SerializeField] private Color enemyColor = new Color(0.90f, 0.30f, 0.25f);
        [SerializeField] private Color deadColor = new Color(0.35f, 0.35f, 0.35f);
        [SerializeField] private Color facingMarkerColor = Color.white;

        [Header("Visual only")]
        [Tooltip("Arena radius shown while no sim exists. Once a run is active the sim's ArenaRadius is used.")]
        [SerializeField, Min(0.1f)] private float defaultArenaRadius = 6f;
        [Tooltip("Dead units vanish after this many seconds (spec: within 1.0 s).")]
        [SerializeField, Min(0f)] private float deadLingerSeconds = 0.8f;

        private sealed class UnitVisual
        {
            public GameObject Root;
            public Transform Marker;
            public Renderer BodyRenderer;
            public float DeadSince = -1f;
        }

        private readonly Dictionary<int, UnitVisual> _visuals = new Dictionary<int, UnitVisual>();
        private readonly List<int> _scratchIds = new List<int>();
        private Transform _arena;
        private float _shownRadius = -1f;
        private ArenaSim _lastSim;
        private Shader _shader;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<GameSessionManager>();
            _shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_shader == null) _shader = Shader.Find("Unlit/Color");
            if (_shader == null) _shader = Shader.Find("Sprites/Default");

            BuildArena();
            SetArenaRadius(defaultArenaRadius);
        }

        private void LateUpdate()
        {
            ArenaSim sim = session != null ? session.ActiveSim : null;

            if (sim != _lastSim)
            {
                ClearUnits(); // new wave / new run: rebuild from the new sim
                _lastSim = sim;
            }

            if (sim == null)
            {
                SetArenaRadius(defaultArenaRadius);
                return;
            }

            SetArenaRadius(sim.Config.ArenaRadius);
            SyncUnits(sim);
        }

        private void OnDestroy()
        {
            ClearUnits();
        }

        // ------------------------------------------------------------------

        private void SyncUnits(ArenaSim sim)
        {
            IReadOnlyList<SimUnit> units = sim.Units;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit u = units[i];
                if (!_visuals.TryGetValue(u.UnitId, out UnitVisual v))
                {
                    v = CreateUnitVisual(u);
                    _visuals.Add(u.UnitId, v);
                }

                if (!u.IsAlive && v.DeadSince < 0f) v.DeadSince = Time.time;
                bool visible = u.IsAlive || Time.time - v.DeadSince < deadLingerSeconds;
                if (v.Root.activeSelf != visible) v.Root.SetActive(visible);
                if (!visible) continue;

                // Position / rotation only; dead units keep sliding (the sim still moves them).
                v.Root.transform.position = new Vector3(u.Position.x, u.Position.y, 0f);
                v.Root.transform.rotation = Quaternion.Euler(0f, 0f, u.Facing >= 0 ? 0f : 180f);

                SetColor(v.BodyRenderer, u.IsAlive ? BaseColor(u) : deadColor);
            }
        }

        private Color BaseColor(SimUnit u) =>
            u.Team == ArenaSim.FighterTeam ? gladiatorColor : enemyColor;

        private UnitVisual CreateUnitVisual(SimUnit u)
        {
            float diameter = u.BodyRadius * 2f;

            GameObject body = CreatePrimitive(PrimitiveType.Sphere,
                (u.Team == ArenaSim.FighterTeam ? "Gladiator_" : "Enemy_") + u.UnitId);
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(diameter, diameter, diameter);

            // Small cube on the local +x side shows which way the unit faces (root flips by 180 deg).
            GameObject marker = CreatePrimitive(PrimitiveType.Cube, "FacingMarker");
            marker.transform.SetParent(body.transform, false);
            marker.transform.localPosition = new Vector3(0.5f, 0f, -0.1f);
            marker.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            Renderer markerRenderer = marker.GetComponent<Renderer>();
            SetColor(markerRenderer, facingMarkerColor);

            return new UnitVisual
            {
                Root = body,
                Marker = marker.transform,
                BodyRenderer = body.GetComponent<Renderer>()
            };
        }

        private void BuildArena()
        {
            // Cylinder axis is Y; rotate 90 deg about X so its flat face looks along Z (top-down 2D view).
            GameObject disc = CreatePrimitive(PrimitiveType.Cylinder, "ArenaBounds");
            disc.transform.SetParent(transform, false);
            disc.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            SetColor(disc.GetComponent<Renderer>(), arenaColor);
            _arena = disc.transform;
        }

        private void SetArenaRadius(float radius)
        {
            if (_arena == null || Mathf.Approximately(radius, _shownRadius)) return;
            _shownRadius = radius;
            // Default cylinder: diameter 1, height 2 -> scale (2R, 0.05 half-height, 2R).
            _arena.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
            _arena.position = new Vector3(0f, 0f, 0.2f); // slightly behind the units
        }

        private GameObject CreatePrimitive(PrimitiveType type, string objectName)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // view only: no physics
            return go;
        }

        private void SetColor(Renderer r, Color color)
        {
            if (r == null) return;
            Material m = r.sharedMaterial;
            if (_shader != null && (m == null || m.shader != _shader))
            {
                m = new Material(_shader);
                r.sharedMaterial = m;
            }
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        private void ClearUnits()
        {
            _scratchIds.Clear();
            foreach (var kv in _visuals) _scratchIds.Add(kv.Key);
            for (int i = 0; i < _scratchIds.Count; i++)
            {
                UnitVisual v = _visuals[_scratchIds[i]];
                if (v.Root != null) Destroy(v.Root);
            }
            _visuals.Clear();
        }
    }
}
