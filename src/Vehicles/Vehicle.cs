using System.Collections.Generic;
using Gearheart.Util;
using Godot;

namespace Gearheart.Vehicles;

/// <summary>
/// Physics approach A: rigid parts joined by joints that can break.
/// A rigid chassis, wheels on spring suspension (groove + damped spring joints),
/// motor torque on the wheels, and wheels that rip off after a hard impact.
/// Everything is built in code so parts can later come from data files.
/// </summary>
public partial class Vehicle : Node2D, IVehicle
{
    public string PhysicsName => "A: rigid parts";
    public Vector2 CenterPosition => Chassis?.GlobalPosition ?? GlobalPosition;

    [Export] public bool PlayerControlled;
    [Export] public float AutoThrottle;                 // used by benchmark bots, -1..1
    [Export] public float WheelTorque = 20000f;
    [Export] public float MaxWheelSpin = 60f;           // rad/s, limits top speed
    [Export] public float AirControlTorque = 15000f;
    [Export] public float BreakImpactSpeed = 1100f;     // sudden speed change (px/s) that rips a wheel off
    [Export] public Color BodyColor = new(0.85f, 0.35f, 0.2f);

    public RigidBody2D Chassis { get; private set; }
    public float Damage { get; private set; }
    public float Speed => Chassis?.LinearVelocity.Length() ?? 0f;

    private static readonly Vector2 ChassisSize = new(130f, 28f);
    private const float WheelRadius = 20f;
    private const float SuspensionTravel = 40f;
    private const float SuspensionRest = 26f;

    private readonly List<Wheel> _wheels = new();
    private Vector2 _lastVelocity;

    private sealed class Wheel
    {
        public RigidBody2D Body;
        public GrooveJoint2D Groove;
        public DampedSpringJoint2D Spring;
        public bool Attached = true;
    }

    public override void _Ready()
    {
        BuildChassis();
        AddWheel(new Vector2(-46f, 6f));
        AddWheel(new Vector2(46f, 6f));
    }

    private void BuildChassis()
    {
        Chassis = new RigidBody2D
        {
            Name = "Chassis",
            Mass = 3f,
            ContactMonitor = true,
            MaxContactsReported = 8,
            ContinuousCd = RigidBody2D.CcdMode.CastShape,
        };

        Chassis.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = ChassisSize } });
        Chassis.AddChild(new Polygon2D { Polygon = Shapes.Rect(ChassisSize), Color = BodyColor });

        var cabin = new[] { new Vector2(-20f, -14f), new Vector2(25f, -14f), new Vector2(10f, -38f), new Vector2(-12f, -38f) };
        Chassis.AddChild(new CollisionPolygon2D { Polygon = cabin });
        Chassis.AddChild(new Polygon2D { Polygon = cabin, Color = BodyColor.Darkened(0.3f) });

        AddChild(Chassis);
        Chassis.BodyEntered += OnChassisHit;
    }

    private void AddWheel(Vector2 anchor)
    {
        var wheel = new RigidBody2D
        {
            Name = $"Wheel{_wheels.Count}",
            Mass = 0.6f,
            Position = anchor + new Vector2(0f, SuspensionRest),
            ContinuousCd = RigidBody2D.CcdMode.CastShape,
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = 1f, Bounce = 0f },
        };
        wheel.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = WheelRadius } });
        wheel.AddChild(new Polygon2D { Polygon = Shapes.Circle(WheelRadius), Color = new Color(0.12f, 0.12f, 0.14f) });
        wheel.AddChild(new Polygon2D { Polygon = Shapes.Circle(WheelRadius * 0.55f), Color = new Color(0.6f, 0.6f, 0.65f) });
        wheel.AddChild(new Line2D
        {
            Points = new[] { Vector2.Zero, new Vector2(WheelRadius * 0.9f, 0f) },
            Width = 3f,
            DefaultColor = new Color(0.9f, 0.9f, 0.9f),
        });
        AddChild(wheel);

        // Both joints sit on the chassis at the anchor and point down (local +Y).
        var groove = new GrooveJoint2D { Position = anchor, Length = SuspensionTravel, InitialOffset = SuspensionRest };
        var spring = new DampedSpringJoint2D
        {
            Position = anchor,
            Length = SuspensionTravel,
            RestLength = SuspensionRest,
            Stiffness = 40f,
            Damping = 1.5f,
        };
        Chassis.AddChild(groove);
        Chassis.AddChild(spring);
        groove.NodeA = groove.GetPathTo(Chassis);
        groove.NodeB = groove.GetPathTo(wheel);
        spring.NodeA = spring.GetPathTo(Chassis);
        spring.NodeB = spring.GetPathTo(wheel);

        _wheels.Add(new Wheel { Body = wheel, Groove = groove, Spring = spring });
    }

    public override void _PhysicsProcess(double delta)
    {
        var input = PlayerControlled ? VehicleInput.ReadPlayer() : VehicleInput.Auto(AutoThrottle);
        float throttle = input.Throttle;
        float tilt = input.Tilt;
        bool handbrake = input.Handbrake;

        foreach (var w in _wheels)
        {
            if (!w.Attached) continue;

            if (handbrake)
            {
                w.Body.AngularVelocity *= 0.85f;
                continue;
            }

            float spin = w.Body.AngularVelocity;
            bool belowTopSpeed = Mathf.Abs(spin) < MaxWheelSpin || Mathf.Sign(spin) != Mathf.Sign(throttle);
            if (!Mathf.IsZeroApprox(throttle) && belowTopSpeed)
                w.Body.ApplyTorque(WheelTorque * throttle); // positive = clockwise = drive right
        }

        if (!Mathf.IsZeroApprox(tilt))
            Chassis.ApplyTorque(AirControlTorque * tilt);

        _lastVelocity = Chassis.LinearVelocity;
    }

    private void OnChassisHit(Node body)
    {
        float impact = (_lastVelocity - Chassis.LinearVelocity).Length();
        if (impact < BreakImpactSpeed * 0.4f) return;

        Damage += impact / 100f;
        if (impact < BreakImpactSpeed) return;

        // Rip off the attached wheel that faces the direction of travel.
        Wheel hit = null;
        float best = float.MinValue;
        Vector2 travel = _lastVelocity.Normalized();
        foreach (var w in _wheels)
        {
            if (!w.Attached) continue;
            float facing = (w.Body.GlobalPosition - Chassis.GlobalPosition).Normalized().Dot(travel);
            if (facing > best)
            {
                best = facing;
                hit = w;
            }
        }

        if (hit != null) Detach(hit);
    }

    private static void Detach(Wheel w)
    {
        w.Attached = false;
        w.Groove.QueueFree();
        w.Spring.QueueFree();
    }
}
