using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Property tests for the pieces that sit where wall runs meet: the arch frames
/// set into a doorway, and the posts capping a crossing. Both exist to make a
/// junction read as one piece of architecture rather than two meshes pushed
/// into each other, so both are checked against the runs around them.
/// </summary>
public sealed class DungeonJunctionGeometryTests
{
    private const float Epsilon = 0.001f;

    /// <summary>
    /// Every arch is a frame set into a wall, never a free-standing gate. Its
    /// posts stand wider than the doorway they span, so each one has to land on
    /// a wall piece: the slot on either side of an arch must be walled.
    /// </summary>
    [Test]
    public void ArchwaysAreFlankedByWallsOnBothSides()
    {
        int archways = 0;
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            foreach (DungeonArchway archway in block.Archways)
            {
                archways++;
                Vector2 along = archway.AlongX ? Vector2.right : Vector2.up;
                foreach (int side in new[] { -1, 1 })
                {
                    Vector2 neighbour = archway.Position + along * (side * DungeonLayout.TileSize);
                    if (!Covers(block.Walls, neighbour, archway.AlongX))
                        Assert.Fail(
                            $"seed {block.Seed}: the arch at {archway.Position} has no wall on "
                                + $"its {(side < 0 ? "near" : "far")} side"
                        );
                }
            }
        }

        Assert.That(archways, Is.GreaterThan(0), "the corpus builds no archways to test");
    }

    /// <summary>
    /// An arch's posts have to reach the walls beside it. The frame is planned
    /// wider than its slot for exactly this reason; if that ever stops being
    /// true the posts float in the doorway with a seam on each side.
    /// </summary>
    [Test]
    public void ArchwayPostsOverlapTheWallsTheyMeet()
    {
        Assert.That(
            DungeonArchway.PostOuterHalfWidth,
            Is.GreaterThan(DungeonLayout.TileSize / 2f),
            "the arch frame is narrower than its slot, so its posts cannot meet the wall"
        );
    }

    /// <summary>
    /// Wherever two runs cross, a post stands on the crossing. Perpendicular
    /// slabs necessarily interpenetrate where they meet - each one straddles
    /// its own centre line - so an uncapped junction shows one run's end cap
    /// pushing through the other's face and the two surfaces fighting over the
    /// same pixels.
    /// </summary>
    [Test]
    public void EveryWallCrossingIsCappedByAPost()
    {
        int crossings = 0;
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            Dictionary<int, List<DungeonWallPiece>> byCentreLine = IndexByCentreLine(block.Walls);
            var posts = new HashSet<(int, int)>();
            foreach (DungeonJunctionPost post in block.Posts)
                posts.Add(Key(post.Position));

            foreach (DungeonWallPiece alongX in block.Walls)
            {
                if (!alongX.AlongX)
                    continue;

                Rect span = alongX.Footprint;
                for (
                    int line = Mathf.FloorToInt(span.xMin);
                    line <= Mathf.CeilToInt(span.xMax);
                    line++
                )
                {
                    if (!byCentreLine.TryGetValue(line, out List<DungeonWallPiece> candidates))
                        continue;

                    foreach (DungeonWallPiece alongZ in candidates)
                    {
                        if (!Overlap(span, alongZ.Footprint, out Rect seam))
                            continue;

                        // A grid corner whose owning run lies outside this block
                        // has no post yet by design; the block simply stops there.
                        var junction = new Vector2(alongZ.Anchor.x, alongX.Anchor.y);
                        if (IsGridCorner(junction, out DungeonEdge owner) && !block.HasEdge(owner))
                            continue;

                        crossings++;

                        // Built rather than asserted per pair: NUnit evaluates a
                        // message eagerly, and this loop runs millions of times.
                        if (!posts.Contains(Key(junction)))
                            Assert.Fail(
                                $"seed {block.Seed}: {alongX} crosses {alongZ} at {junction} "
                                    + "with no post capping the seam"
                            );
                        if (!Contains(new DungeonJunctionPost(junction).Footprint, seam))
                            Assert.Fail(
                                $"seed {block.Seed}: the post at {junction} does not cover the "
                                    + $"seam {seam} between {alongX} and {alongZ}"
                            );
                    }
                }
            }
        }

        Assert.That(crossings, Is.GreaterThan(0), "the corpus builds no crossings to test");
    }

    /// <summary>Walls running along Z, bucketed by the whole unit their centre line sits on.</summary>
    private static Dictionary<int, List<DungeonWallPiece>> IndexByCentreLine(
        List<DungeonWallPiece> walls
    )
    {
        var index = new Dictionary<int, List<DungeonWallPiece>>();
        foreach (DungeonWallPiece piece in walls)
        {
            if (piece.AlongX)
                continue;

            int line = Mathf.RoundToInt(piece.Anchor.x);
            if (!index.TryGetValue(line, out List<DungeonWallPiece> bucket))
            {
                bucket = new List<DungeonWallPiece>();
                index[line] = bucket;
            }
            bucket.Add(piece);
        }
        return index;
    }

    private static (int, int) Key(Vector2 position)
    {
        return (Mathf.RoundToInt(position.x * 100f), Mathf.RoundToInt(position.y * 100f));
    }

    /// <summary>
    /// A post caps a crossing and nothing else. One standing where no two runs
    /// meet is an obstacle in the open, so the set has to be exact in both
    /// directions rather than merely large enough.
    /// </summary>
    [Test]
    public void PostsOnlyStandOnCrossings()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            Dictionary<int, List<DungeonWallPiece>> byCentreLine = IndexByCentreLine(block.Walls);
            foreach (DungeonJunctionPost post in block.Posts)
            {
                bool onZ = false;
                Rect footprint = post.Footprint;
                for (
                    int line = Mathf.FloorToInt(footprint.xMin);
                    line <= Mathf.CeilToInt(footprint.xMax);
                    line++
                )
                {
                    if (!byCentreLine.TryGetValue(line, out List<DungeonWallPiece> candidates))
                        continue;
                    onZ |= candidates.Exists(piece => Overlap(piece.Footprint, footprint, out _));
                }

                if (!onZ)
                    Assert.Fail($"seed {block.Seed}: the post at {post.Position} caps no crossing");
            }
        }
    }

    /// <summary>Two posts never stand in the same place.</summary>
    [Test]
    public void NoPostIsBuiltTwice()
    {
        foreach (DungeonGeometryModel block in DungeonGeometryModel.Blocks())
        {
            var seen = new HashSet<(int, int)>();
            foreach (DungeonJunctionPost post in block.Posts)
            {
                if (!seen.Add(Key(post.Position)))
                    Assert.Fail($"seed {block.Seed}: two posts stand at {post.Position}");
            }
        }
    }

    /// <summary>The post has to reach over the seam it hides, and over its top.</summary>
    [Test]
    public void PostsAreBiggerThanTheSeamsTheyHide()
    {
        Assert.That(
            DungeonJunctionPost.HalfWidth,
            Is.GreaterThan(DungeonWallPiece.SlabHalfThickness),
            "a post no wider than a slab leaves the seam showing along its edge"
        );
        Assert.That(
            DungeonJunctionPost.Height,
            Is.GreaterThanOrEqualTo(DungeonWallPiece.SlabHeight),
            "a post shorter than the walls leaves the seam showing above it"
        );
    }

    /// <summary>The shared rectangle of two footprints, when they have one.</summary>
    private static bool Overlap(Rect a, Rect b, out Rect shared)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        shared = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return xMax - xMin > Epsilon && yMax - yMin > Epsilon;
    }

    private static bool Contains(Rect outer, Rect inner)
    {
        return inner.xMin >= outer.xMin - Epsilon
            && inner.xMax <= outer.xMax + Epsilon
            && inner.yMin >= outer.yMin - Epsilon
            && inner.yMax <= outer.yMax + Epsilon;
    }

    /// <summary>
    /// The horizontal run that owns a point, when the point is one of the grid
    /// corners where four boundary runs meet.
    /// </summary>
    private static bool IsGridCorner(Vector2 junction, out DungeonEdge owner)
    {
        float x = (junction.x - DungeonLayout.RoomWidth / 2f) / DungeonLayout.RoomWidth;
        float y = (junction.y - DungeonLayout.RoomDepth / 2f) / DungeonLayout.RoomDepth;
        owner = new DungeonEdge(Mathf.RoundToInt(x), Mathf.RoundToInt(y), true);
        return Mathf.Abs(x - Mathf.Round(x)) < 0.0001f && Mathf.Abs(y - Mathf.Round(y)) < 0.0001f;
    }

    /// <summary>Whether any piece running the given way covers a point.</summary>
    private static bool Covers(List<DungeonWallPiece> walls, Vector2 point, bool alongX)
    {
        foreach (DungeonWallPiece piece in walls)
        {
            Rect footprint = piece.Footprint;
            if (
                piece.AlongX == alongX
                && point.x >= footprint.xMin - Epsilon
                && point.x <= footprint.xMax + Epsilon
                && point.y >= footprint.yMin - Epsilon
                && point.y <= footprint.yMax + Epsilon
            )
                return true;
        }
        return false;
    }
}
