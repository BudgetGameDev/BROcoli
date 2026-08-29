using UnityEngine;

/// <summary>
/// Ground rectangles lifted into world-space boxes, and the ray test the
/// simulation leans on. A sweep casts millions of these, so the slab test is
/// written in managed code rather than crossing into the engine each time.
/// </summary>
internal static class WallVisibilityBounds
{
    /// <summary>Lifts a ground rectangle into a world-space box.</summary>
    public static Bounds Lift(Rect footprint, float centerY, float height)
    {
        return new Bounds(
            new Vector3(footprint.center.x, centerY, footprint.center.y),
            new Vector3(footprint.width, height, footprint.height)
        );
    }

    /// <summary>A managed slab test; the sweep runs millions of these.</summary>
    public static bool IntersectsRay(Bounds bounds, Ray ray, float maximumDistance)
    {
        float enter = 0f;
        float exit = maximumDistance;
        Vector3 origin = ray.origin;
        Vector3 direction = ray.direction;
        for (int axis = 0; axis < 3; axis++)
        {
            float step = direction[axis];
            float minimum = bounds.min[axis];
            float maximum = bounds.max[axis];
            if (Mathf.Abs(step) < 1e-8f)
            {
                if (origin[axis] < minimum || origin[axis] > maximum)
                    return false;
                continue;
            }

            float first = (minimum - origin[axis]) / step;
            float second = (maximum - origin[axis]) / step;
            if (first > second)
                (first, second) = (second, first);
            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, second);
            if (enter > exit)
                return false;
        }
        return true;
    }
}
