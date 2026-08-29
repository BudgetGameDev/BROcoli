using NUnit.Framework;

public sealed class LootChestExperienceTests
{
    [TestCase(1, 10)]
    [TestCase(2, 15)]
    [TestCase(3, 20)]
    [TestCase(5, 30)]
    public void ExperiencePerPickupGrowsWithDungeonRing(int ring, int expected)
    {
        Assert.That(LootChest.ScaledExperiencePerPickup(10, 0.5f, ring), Is.EqualTo(expected));
    }

    [Test]
    public void ExperiencePerPickupNeverDropsBelowOne()
    {
        Assert.That(LootChest.ScaledExperiencePerPickup(0, -1f, 0), Is.EqualTo(1));
    }
}
