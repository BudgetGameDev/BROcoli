using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Draws the per-kind symbol onto a boost token's face out of simple boxes.
    /// </summary>
    public sealed partial class PickupVisual3D
    {
        internal void BuildSymbol(
            Transform face,
            ModelKind kind,
            Color symbolColor,
            Color accentColor
        )
        {
            const float faceDepth = -0.145f;

            switch (kind)
            {
                case ModelKind.Health:
                    AddBox(
                        face,
                        "Health Vertical",
                        new Vector3(0f, 0f, faceDepth),
                        new Vector3(0.14f, 0.54f, 0.075f),
                        0f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Health Horizontal",
                        new Vector3(0f, 0f, faceDepth),
                        new Vector3(0.54f, 0.14f, 0.075f),
                        0f,
                        symbolColor
                    );
                    break;

                case ModelKind.Damage:
                    AddBox(
                        face,
                        "Blade",
                        new Vector3(0.02f, 0.03f, faceDepth),
                        new Vector3(0.13f, 0.58f, 0.075f),
                        -24f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Guard",
                        new Vector3(-0.08f, -0.18f, faceDepth - 0.005f),
                        new Vector3(0.4f, 0.09f, 0.085f),
                        -24f,
                        accentColor
                    );
                    AddBox(
                        face,
                        "Pommel",
                        new Vector3(-0.17f, -0.34f, faceDepth),
                        new Vector3(0.13f, 0.13f, 0.08f),
                        -24f,
                        symbolColor
                    );
                    break;

                case ModelKind.MovementSpeed:
                    AddChevron(face, -0.11f, faceDepth, symbolColor);
                    AddChevron(face, 0.16f, faceDepth, symbolColor);
                    break;

                case ModelKind.AttackSpeed:
                    CreatePart(
                        face,
                        "Attack Dial",
                        GetRingMesh(),
                        new Vector3(0f, 0f, faceDepth),
                        Quaternion.identity,
                        new Vector3(0.6f, 0.6f, 0.07f),
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Attack Hand",
                        new Vector3(0.07f, 0.09f, faceDepth - 0.015f),
                        new Vector3(0.09f, 0.34f, 0.09f),
                        -34f,
                        accentColor
                    );
                    AddBox(
                        face,
                        "Attack Tick",
                        new Vector3(-0.2f, 0.19f, faceDepth - 0.01f),
                        new Vector3(0.08f, 0.15f, 0.08f),
                        -44f,
                        symbolColor
                    );
                    break;

                case ModelKind.ExperienceBoost:
                    AddBox(
                        face,
                        "XP Diamond",
                        new Vector3(0f, 0f, faceDepth),
                        new Vector3(0.36f, 0.36f, 0.1f),
                        45f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "XP Spark",
                        new Vector3(0.22f, 0.22f, faceDepth - 0.015f),
                        new Vector3(0.1f, 0.1f, 0.08f),
                        45f,
                        accentColor
                    );
                    break;

                case ModelKind.DetectionRadius:
                    CreatePart(
                        face,
                        "Radar Outer",
                        GetRingMesh(),
                        new Vector3(0f, 0f, faceDepth),
                        Quaternion.identity,
                        new Vector3(0.65f, 0.65f, 0.065f),
                        symbolColor
                    );
                    CreatePart(
                        face,
                        "Radar Inner",
                        GetRingMesh(),
                        new Vector3(0f, 0f, faceDepth - 0.01f),
                        Quaternion.identity,
                        new Vector3(0.34f, 0.34f, 0.08f),
                        accentColor
                    );
                    AddBox(
                        face,
                        "Radar Sweep",
                        new Vector3(0.08f, 0.09f, faceDepth - 0.02f),
                        new Vector3(0.07f, 0.34f, 0.09f),
                        -42f,
                        symbolColor
                    );
                    break;

                case ModelKind.Magnet:
                    AddBox(
                        face,
                        "Magnet Bridge",
                        new Vector3(0f, -0.2f, faceDepth),
                        new Vector3(0.5f, 0.13f, 0.085f),
                        0f,
                        accentColor
                    );
                    AddBox(
                        face,
                        "Magnet Left",
                        new Vector3(-0.19f, 0f, faceDepth),
                        new Vector3(0.13f, 0.42f, 0.085f),
                        0f,
                        accentColor
                    );
                    AddBox(
                        face,
                        "Magnet Right",
                        new Vector3(0.19f, 0f, faceDepth),
                        new Vector3(0.13f, 0.42f, 0.085f),
                        0f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Magnet Left Tip",
                        new Vector3(-0.19f, 0.25f, faceDepth - 0.01f),
                        new Vector3(0.15f, 0.12f, 0.095f),
                        0f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Magnet Right Tip",
                        new Vector3(0.19f, 0.25f, faceDepth - 0.01f),
                        new Vector3(0.15f, 0.12f, 0.095f),
                        0f,
                        Color.white
                    );
                    break;

                case ModelKind.Hourglass:
                    AddBox(
                        face,
                        "Hourglass Top",
                        new Vector3(0f, 0.28f, faceDepth),
                        new Vector3(0.52f, 0.08f, 0.075f),
                        0f,
                        accentColor
                    );
                    AddBox(
                        face,
                        "Hourglass Bottom",
                        new Vector3(0f, -0.28f, faceDepth),
                        new Vector3(0.52f, 0.08f, 0.075f),
                        0f,
                        accentColor
                    );
                    AddBox(
                        face,
                        "Hourglass Left",
                        new Vector3(-0.11f, 0f, faceDepth),
                        new Vector3(0.08f, 0.5f, 0.065f),
                        -24f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Hourglass Right",
                        new Vector3(0.11f, 0f, faceDepth),
                        new Vector3(0.08f, 0.5f, 0.065f),
                        24f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Hourglass Sand",
                        new Vector3(0f, -0.1f, faceDepth - 0.015f),
                        new Vector3(0.13f, 0.2f, 0.09f),
                        45f,
                        accentColor
                    );
                    break;

                case ModelKind.SprayRange:
                    AddBox(
                        face,
                        "Range Stem",
                        new Vector3(-0.08f, -0.08f, faceDepth),
                        new Vector3(0.1f, 0.5f, 0.075f),
                        -35f,
                        symbolColor
                    );
                    AddChevron(face, 0.18f, faceDepth - 0.01f, accentColor);
                    break;

                case ModelKind.SprayWidth:
                    AddBox(
                        face,
                        "Width Left",
                        new Vector3(-0.13f, 0f, faceDepth),
                        new Vector3(0.09f, 0.54f, 0.075f),
                        -20f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Width Right",
                        new Vector3(0.13f, 0f, faceDepth),
                        new Vector3(0.09f, 0.54f, 0.075f),
                        20f,
                        symbolColor
                    );
                    AddBox(
                        face,
                        "Width Base",
                        new Vector3(0f, -0.22f, faceDepth - 0.01f),
                        new Vector3(0.42f, 0.09f, 0.085f),
                        0f,
                        accentColor
                    );
                    break;
            }
        }
    }
}
