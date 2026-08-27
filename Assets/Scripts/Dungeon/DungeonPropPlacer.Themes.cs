using System.Collections.Generic;
using UnityEngine;

public partial class DungeonPropPlacer
{
    private void BuildThemeProps(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied
    )
    {
        switch (archetype.Theme)
        {
            case DungeonLayout.RoomTheme.Empty:
                return;
            case DungeonLayout.RoomTheme.Sparse:
                Scatter(
                    parent,
                    center,
                    archetype,
                    random,
                    occupied,
                    random.Next(0, 4),
                    "Barrel",
                    "Pot",
                    "Chair",
                    "Stones"
                );
                break;
            case DungeonLayout.RoomTheme.Storage:
                Scatter(
                    parent,
                    center,
                    archetype,
                    random,
                    occupied,
                    9 + random.Next(0, 6),
                    "Barrel",
                    "Pot",
                    "WoodSupport",
                    "WoodStructure",
                    "Table"
                );
                PlaceWallBanner(parent, center, archetype, archetype.Variant);
                break;
            case DungeonLayout.RoomTheme.Banquet:
                BuildBanquet(parent, center, archetype, random, occupied);
                break;
            case DungeonLayout.RoomTheme.Armory:
                BuildArmory(parent, center, archetype, random, occupied);
                break;
            case DungeonLayout.RoomTheme.Shrine:
                BuildShrine(parent, center, archetype, occupied);
                break;
            case DungeonLayout.RoomTheme.Flooded:
                Scatter(
                    parent,
                    center,
                    archetype,
                    random,
                    occupied,
                    3 + random.Next(0, 4),
                    "Rocks",
                    "Stones",
                    "Dirt"
                );
                break;
            case DungeonLayout.RoomTheme.TreasureVault:
                BuildVault(parent, center, archetype, random, occupied);
                break;
            case DungeonLayout.RoomTheme.Collapsed:
                BuildCollapsed(parent, center, archetype, random, occupied);
                break;
        }
    }

    private void BuildBanquet(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied
    )
    {
        bool horizontal = archetype.Shape == DungeonLayout.RoomShape.LongHorizontal;
        float[] stations = horizontal ? new[] { -6f, 0f, 6f } : new[] { -3.4f, 3.4f };
        foreach (float station in stations)
        {
            Vector2 table = horizontal ? new Vector2(station, 0f) : new Vector2(0f, station);
            PlaceNamed(parent, center, "Table", table, horizontal ? 0f : 90f, occupied);
            Vector2 side = horizontal ? Vector2.up * 1.55f : Vector2.right * 1.55f;
            PlaceNamed(parent, center, "Chair", table + side, horizontal ? 180f : -90f, occupied);
            PlaceNamed(parent, center, "Chair", table - side, horizontal ? 0f : 90f, occupied);
        }
        PlaceWallBanner(parent, center, archetype, archetype.Variant);
        PlaceWallBanner(parent, center, archetype, archetype.Variant + 2);
        Scatter(
            parent,
            center,
            archetype,
            random,
            occupied,
            2 + random.Next(0, 3),
            "Pot",
            "Potion",
            "Barrel"
        );
    }

    private void BuildArmory(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied
    )
    {
        float w = Mathf.Max(3.3f, archetype.HalfWidth - 1.2f);
        float d = Mathf.Max(3.2f, archetype.HalfDepth - 1.1f);
        string[] display = { "ShieldRound", "WeaponSword", "ShieldRectangle", "WeaponSpear" };
        for (int i = 0; i < display.Length; i++)
        {
            float x = Mathf.Lerp(-w, w, (i + 0.5f) / display.Length);
            PlaceNamed(parent, center, display[i], new Vector2(x, d), 0f, occupied);
            PlaceNamed(
                parent,
                center,
                display[(i + 2) % display.Length],
                new Vector2(x, -d),
                180f,
                occupied
            );
        }
        PlaceNamed(parent, center, "Trap", new Vector2(-2.8f, -2.4f), 45f, occupied);
        PlaceNamed(parent, center, "Trap", new Vector2(2.8f, 2.4f), 45f, occupied);
        Scatter(
            parent,
            center,
            archetype,
            random,
            occupied,
            2 + random.Next(0, 4),
            "WoodSupport",
            "Barrel",
            "Stones"
        );
    }

    private void BuildShrine(
        Transform parent,
        Vector2 center,
        DungeonLayout.RoomArchetype archetype,
        List<Vector2> occupied
    )
    {
        float x = Mathf.Min(3.6f, archetype.HalfWidth - 0.9f);
        float z = Mathf.Min(3.6f, archetype.HalfDepth - 0.9f);
        foreach (
            Vector2 p in new[]
            {
                new Vector2(-x, -z),
                new Vector2(x, -z),
                new Vector2(-x, z),
                new Vector2(x, z),
            }
        )
            PlaceNamed(parent, center, "Column", p, 0f, occupied);

        PlaceNamed(parent, center, "Stairs", Vector2.zero, archetype.Variant * 90f, occupied, 1.2f);
        string offering = (archetype.Variant % 3) switch
        {
            0 => "Potion",
            1 => "Coin",
            _ => "Key",
        };
        PlaceNamed(
            parent,
            center,
            offering,
            Vector2.zero,
            archetype.Variant * 90f,
            occupied,
            1f,
            2.42f
        );
        PlaceWallBanner(parent, center, archetype, archetype.Variant);
    }
}
