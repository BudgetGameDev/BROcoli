using NUnit.Framework;
using UnityEngine;

public sealed class VirtualJoystickMathTests
{
    [Test]
    public void StickTravelProducesGradualAnalogMagnitudes()
    {
        float nearCenter = VirtualJoystickMath
            .AnalogInput(Vector2.right * 10f, 100f, 0.06f, 1.25f)
            .x;
        float quarter = VirtualJoystickMath.AnalogInput(Vector2.right * 25f, 100f, 0.06f, 1.25f).x;
        float half = VirtualJoystickMath.AnalogInput(Vector2.right * 50f, 100f, 0.06f, 1.25f).x;
        float full = VirtualJoystickMath.AnalogInput(Vector2.right * 100f, 100f, 0.06f, 1.25f).x;

        Assert.That(nearCenter, Is.GreaterThan(0f).And.LessThan(quarter));
        Assert.That(quarter, Is.GreaterThan(nearCenter).And.LessThan(half));
        Assert.That(half, Is.GreaterThan(quarter).And.LessThan(full));
        Assert.That(full, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void DeadZoneRejectsOnlySmallCentralTravel()
    {
        Vector2 inside = VirtualJoystickMath.AnalogInput(Vector2.right * 5f, 100f, 0.06f, 1.25f);
        Vector2 outside = VirtualJoystickMath.AnalogInput(Vector2.right * 7f, 100f, 0.06f, 1.25f);

        Assert.That(inside, Is.EqualTo(Vector2.zero));
        Assert.That(outside.magnitude, Is.GreaterThan(0f));
    }

    [Test]
    public void DirectionIsPreserved()
    {
        Vector2 input = VirtualJoystickMath.AnalogInput(new Vector2(30f, 40f), 100f, 0.06f, 1.25f);

        Assert.That(Vector2.Angle(input, new Vector2(3f, 4f)), Is.LessThan(0.01f));
    }
}
