namespace DynamicIsland.Core;

/// <summary>Damped spring (mass 1). Defaults give ζ≈0.66 → a small, macOS-like overshoot.</summary>
public sealed class Spring
{
    private const double MaxSubStep = 1.0 / 240;
    private const double Epsilon = 0.05;
    private readonly double _k;
    private readonly double _c;

    public Spring(double value, double stiffness = 280, double damping = 22)
    {
        Value = value;
        Target = value;
        _k = stiffness;
        _c = damping;
    }

    public double Value { get; private set; }
    public double Velocity { get; private set; }
    public double Target { get; set; }

    public bool IsSettled => Math.Abs(Target - Value) < Epsilon && Math.Abs(Velocity) < Epsilon;

    /// <summary>Jump straight to the target (no animation).</summary>
    public void Snap()
    {
        Value = Target;
        Velocity = 0;
    }

    public void Step(double dtSeconds)
    {
        if (dtSeconds <= 0) return;
        var steps = (int)Math.Ceiling(dtSeconds / MaxSubStep);
        var h = dtSeconds / steps;
        for (var i = 0; i < steps; i++)
        {
            var a = -_k * (Value - Target) - _c * Velocity;
            Velocity += a * h;          // semi-implicit Euler: velocity first,
            Value += Velocity * h;      // then position with the new velocity
        }
        if (IsSettled) { Value = Target; Velocity = 0; }
    }
}
