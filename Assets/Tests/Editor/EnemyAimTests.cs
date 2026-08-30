using NUnit.Framework;
using UnityEngine;

public sealed class EnemyAimTests
{
    [Test]
    public void StationaryTargetUsesDirectAim()
    {
        Vector2 direction = ShootingEnemyScript.CalculateAimDirection(
            Vector2.zero,
            new Vector2(4f, 3f),
            Vector2.zero,
            5f,
            1f,
            2f
        );

        Assert.That(Vector2.Angle(direction, new Vector2(4f, 3f)), Is.LessThan(0.01f));
    }

    [Test]
    public void LateralMovementIsLed()
    {
        Vector2 direction = ShootingEnemyScript.CalculateAimDirection(
            Vector2.zero,
            new Vector2(10f, 0f),
            new Vector2(0f, 3f),
            7f,
            1f,
            3f
        );

        Assert.That(direction.x, Is.GreaterThan(0f));
        Assert.That(direction.y, Is.GreaterThan(0f));
    }

    [Test]
    public void AimCorrectsForOffsetProjectileOrigin()
    {
        Vector2 direction = ShootingEnemyScript.CalculateAimDirection(
            new Vector2(0f, 0.2f),
            new Vector2(10f, 0f),
            Vector2.zero,
            5f,
            1f,
            2f
        );

        Assert.That(direction.x, Is.GreaterThan(0f));
        Assert.That(direction.y, Is.LessThan(0f));
    }

    [Test]
    public void LeadTimeIsCapped()
    {
        Vector2 direction = ShootingEnemyScript.CalculateAimDirection(
            Vector2.zero,
            new Vector2(10f, 0f),
            new Vector2(0f, 4f),
            5f,
            1f,
            0.5f
        );

        Vector2 expected = new Vector2(10f, 2f).normalized;
        Assert.That(Vector2.Angle(direction, expected), Is.LessThan(0.01f));
    }
}
