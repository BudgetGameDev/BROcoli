using NUnit.Framework;
using UnityEngine;

public sealed class ProjectileWallCollisionTests
{
    private GameObject wall;

    [SetUp]
    public void SetUp()
    {
        wall = new GameObject("Projectile wall test");
        wall.layer = LayerMask.NameToLayer("Wall");
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.5f, 2f, 4f);
        wall.transform.position = new Vector3(0f, 1f, 0f);
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(wall);
    }

    [Test]
    public void SolidWallBlocksLineOfSightForEitherDirection()
    {
        Vector3 left = new Vector3(-2f, 0.5f, 0f);
        Vector3 right = new Vector3(2f, 0.5f, 0f);

        Assert.That(ProjectileWallCollision.HasClearLine(left, right), Is.False);
        Assert.That(ProjectileWallCollision.HasClearLine(right, left), Is.False);
    }

    [Test]
    public void ClearLineOfSightRemainsShootable()
    {
        Vector3 left = new Vector3(-2f, 0.5f, 3f);
        Vector3 right = new Vector3(2f, 0.5f, 3f);

        Assert.That(ProjectileWallCollision.HasClearLine(left, right), Is.True);
    }

    [Test]
    public void TriggerOnWallLayerDoesNotBlockShots()
    {
        wall.GetComponent<BoxCollider>().isTrigger = true;
        Physics.SyncTransforms();

        Assert.That(
            ProjectileWallCollision.HasClearLine(
                new Vector3(-2f, 0.5f, 0f),
                new Vector3(2f, 0.5f, 0f)
            ),
            Is.True
        );
    }
}
