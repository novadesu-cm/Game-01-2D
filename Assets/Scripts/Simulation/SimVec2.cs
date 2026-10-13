using System;

namespace GotA.Simulation
{
    /// <summary>
    /// Simulation-side 2D vector. Pure C# (no UnityEngine). Convert to
    /// UnityEngine.Vector2 only inside ArenaView.
    /// </summary>
    public readonly struct SimVec2 : IEquatable<SimVec2>
    {
        public readonly float x;
        public readonly float y;

        public SimVec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static SimVec2 Zero => new SimVec2(0f, 0f);

        public float SqrMagnitude => x * x + y * y;

        public float Magnitude => MathF.Sqrt(x * x + y * y);

        /// <summary>Unit-length copy, or Zero when the vector has zero length.</summary>
        public SimVec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 0f ? new SimVec2(x / m, y / m) : Zero;
            }
        }

        public static float Dot(SimVec2 a, SimVec2 b) => a.x * b.x + a.y * b.y;

        public static float Distance(SimVec2 a, SimVec2 b) => (a - b).Magnitude;

        public static SimVec2 operator +(SimVec2 a, SimVec2 b) => new SimVec2(a.x + b.x, a.y + b.y);

        public static SimVec2 operator -(SimVec2 a, SimVec2 b) => new SimVec2(a.x - b.x, a.y - b.y);

        public static SimVec2 operator -(SimVec2 a) => new SimVec2(-a.x, -a.y);

        public static SimVec2 operator *(SimVec2 a, float s) => new SimVec2(a.x * s, a.y * s);

        public static SimVec2 operator *(float s, SimVec2 a) => new SimVec2(a.x * s, a.y * s);

        public static SimVec2 operator /(SimVec2 a, float s) => new SimVec2(a.x / s, a.y / s);

        public static bool operator ==(SimVec2 a, SimVec2 b) => a.x == b.x && a.y == b.y;

        public static bool operator !=(SimVec2 a, SimVec2 b) => !(a == b);

        public bool Equals(SimVec2 other) => this == other;

        public override bool Equals(object obj) => obj is SimVec2 other && this == other;

        public override int GetHashCode() => HashCode.Combine(x, y);

        public override string ToString() => "(" + x.ToString("R") + ", " + y.ToString("R") + ")";
    }
}
