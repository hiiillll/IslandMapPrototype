using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level03ActivePlanSplineRoadRequestRunner
{
    private const string RequestPath =
        "Assets/Level03/REBUILD_ACTIVE_PLAN_SPLINE_ROADS.request";

    static Level03ActivePlanSplineRoadRequestRunner()
    {
        EditorApplication.delayCall += TryRun;
    }

    private static void TryRun()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }

        try
        {
            Level03ActivePlanSplineRoadRebuilder.Rebuild();
            Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            AssetDatabase.DeleteAsset(RequestPath);
        }
    }
}

/// <summary>
/// Derives a one-pixel skeleton from the current active road plan and turns
/// each graph edge into a Catmull-Rom ribbon. The same source image supplies
/// the left/right width at every sample, keeping the authored layout intact.
/// </summary>
public static class Level03ActivePlanSplineRoadGenerator
{
    private const float SimplificationTolerance = 2f;
    private const float SkeletonThreshold = 0.15f;
    private const float SplineSampleSpacing = 2.5f;
    private const float WidthProbeStep = 1f;
    private const float MinimumHalfWidth = 3f;
    private const float MaximumHalfWidth = 32f;
    // Edges shorter than this are medial-axis branches inside a wide painted
    // endpoint, not authored road segments. Keeping them creates round-looking
    // side lobes even when the ribbon itself uses a straight end cut.
    private const float MinimumPathLength = 18f;
    private const int ControlSmoothRadius = 2;
    private const int ControlSmoothPasses = 2;
    private const int TangentSampleSpan = 1;
    private const int WidthSmoothRadius = 24;
    private const int JunctionWidthBlendSamples = 32;
    private const int WidthSpikeWindow = 28;
    private const float MaximumLocalWidthRatio = 1.15f;
    private const int CapSegments = 20;
    private const float MarkingMinimumRoadWidth = 25f;
    private const float MarkingDashLength = 18f;
    private const float MarkingGapLength = 14f;
    private const float MarkingHalfWidth = 1.35f;
    // Skeleton graph nodes also occur at ordinary T-junctions. Half a normal
    // dash gap on each adjoining path keeps the centre line visually regular
    // without painting through a broad intersection.
    private const float MarkingJunctionClearance = MarkingGapLength * 0.5f;

    private static readonly int[] NeighbourX = { -1, 0, 1, -1, 1, -1, 0, 1 };
    private static readonly int[] NeighbourY = { -1, -1, -1, 0, 0, 1, 1, 1 };

    public static Mesh Build(
        Texture2D roadPlan,
        float worldWidth,
        float worldDepth,
        float roadHeight,
        float threshold)
    {
        if (roadPlan == null)
        {
            throw new ArgumentNullException(nameof(roadPlan));
        }

        SkeletonGraph graph = new SkeletonGraph(
            roadPlan,
            Mathf.Min(threshold, SkeletonThreshold));
        List<List<int>> pixelPaths = graph.TracePaths();
        List<Vector3> vertices = new List<Vector3>(32000);
        List<Vector2> uvs = new List<Vector2>(32000);
        List<int> triangles = new List<int>(48000);
        int generatedPaths = 0;
        int skippedPaths = 0;
        int[] generatedByQuarter = new int[4];

        foreach (List<int> pixelPath in pixelPaths)
        {
            List<Vector2> controls = ConvertToWorldPoints(
                pixelPath,
                graph.Width,
                graph.Height,
                worldWidth,
                worldDepth);
            if (CalculateLength(controls) < MinimumPathLength)
            {
                skippedPaths++;
                continue;
            }

            bool closed = controls.Count > 2 &&
                          Vector2.Distance(controls[0], controls[controls.Count - 1]) <= 0.1f;
            // Keep authored dead ends and bridge landings as straight, width-matched
            // cuts. Round caps from several traced paths can overlap at a graph node
            // and create the large circular protrusions visible in close-up.
            bool capStart = false;
            bool capEnd = false;
            controls = Simplify(controls, SimplificationTolerance);
            controls = SmoothControls(
                controls,
                closed,
                roadPlan,
                worldWidth,
                worldDepth,
                threshold);
            List<Vector2> samples = SampleSpline(
                controls,
                closed,
                SplineSampleSpacing,
                roadPlan,
                worldWidth,
                worldDepth,
                threshold);
            if (samples.Count < 2)
            {
                continue;
            }

            AddRibbon(
                samples,
                closed,
                capStart,
                capEnd,
                roadPlan,
                worldWidth,
                worldDepth,
                roadHeight,
                threshold,
                vertices,
                uvs,
                triangles);
            float averageZ = 0f;
            foreach (Vector2 sample in samples)
            {
                averageZ += sample.y;
            }
            averageZ /= samples.Count;
            int quarter = Mathf.Clamp(
                Mathf.FloorToInt((0.5f - averageZ / worldDepth) * 4f),
                0,
                3);
            generatedByQuarter[quarter]++;
            generatedPaths++;
        }

        if (triangles.Count == 0)
        {
            throw new InvalidOperationException(
                "No spline paths could be extracted from the active Level03 road plan.");
        }

        int[] upwardTriangles = new int[4];
        int[] downwardTriangles = new int[4];
        for (int triangle = 0; triangle < triangles.Count; triangle += 3)
        {
            Vector3 first = vertices[triangles[triangle]];
            Vector3 second = vertices[triangles[triangle + 1]];
            Vector3 third = vertices[triangles[triangle + 2]];
            float normalY = Vector3.Cross(second - first, third - first).y;
            float centreZ = (first.z + second.z + third.z) / 3f;
            int quarter = Mathf.Clamp(
                Mathf.FloorToInt((0.5f - centreZ / worldDepth) * 4f),
                0,
                3);
            if (normalY >= 0f)
            {
                upwardTriangles[quarter]++;
            }
            else
            {
                downwardTriangles[quarter]++;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "MESH_Level03_Roads",
            indexFormat = IndexFormat.UInt32
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Debug.Log(
            $"[Level03 Active-Plan Spline Roads] Generated {generatedPaths} spline paths " +
            $"from {pixelPaths.Count} traced skeleton paths; skipped {skippedPaths}. " +
            $"Paths by image quarter: {string.Join(",", generatedByQuarter)}.");
        Debug.Log(
            $"[Level03 Active-Plan Spline Roads] Upward triangles by quarter: " +
            $"{string.Join(",", upwardTriangles)}; downward: " +
            $"{string.Join(",", downwardTriangles)}.");
        return mesh;
    }

    public static Mesh BuildMarkings(
        Texture2D roadPlan,
        float worldWidth,
        float worldDepth,
        float roadHeight,
        float threshold)
    {
        SkeletonGraph graph = new SkeletonGraph(
            roadPlan,
            Mathf.Min(threshold, SkeletonThreshold));
        List<Vector3> vertices = new List<Vector3>(12000);
        List<Vector2> uvs = new List<Vector2>(12000);
        List<int> triangles = new List<int>(18000);
        int markedPaths = 0;
        int dashCount = 0;

        foreach (List<int> pixelPath in graph.TracePaths())
        {
            List<Vector2> controls = ConvertToWorldPoints(
                pixelPath,
                graph.Width,
                graph.Height,
                worldWidth,
                worldDepth);
            bool closed = controls.Count > 2 &&
                          Vector2.Distance(controls[0], controls[controls.Count - 1]) <= 0.1f;
            controls = SmoothControls(
                Simplify(controls, SimplificationTolerance),
                closed,
                roadPlan,
                worldWidth,
                worldDepth,
                threshold);
            List<Vector2> samples = SampleSpline(
                controls,
                closed,
                SplineSampleSpacing,
                roadPlan,
                worldWidth,
                worldDepth,
                threshold);
            if (samples.Count < 2)
            {
                continue;
            }

            int pathDashCount = AddDashedCenterline(
                samples,
                closed,
                roadHeight + 0.035f,
                roadPlan,
                worldWidth,
                worldDepth,
                threshold,
                vertices,
                uvs,
                triangles);
            if (pathDashCount > 0)
            {
                markedPaths++;
                dashCount += pathDashCount;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "MESH_Level03_RoadMarkings",
            indexFormat = IndexFormat.UInt32
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Debug.Log(
            $"[Level03 Road Markings] Generated {markedPaths} marked main-road paths " +
            $"with {dashCount} dashes and {triangles.Count / 3:N0} triangles.");
        return mesh;
    }

    private static int AddDashedCenterline(
        List<Vector2> samples,
        bool closed,
        float height,
        Texture2D roadPlan,
        float worldWidth,
        float worldDepth,
        float threshold,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        float[] distances = new float[samples.Count];
        for (int index = 1; index < samples.Count; index++)
        {
            distances[index] = distances[index - 1] +
                               Vector2.Distance(samples[index - 1], samples[index]);
        }

        float totalLength = distances[distances.Length - 1];
        float start = closed ? 0f : MarkingJunctionClearance;
        float end = closed ? totalLength : totalLength - MarkingJunctionClearance;
        int dashCount = 0;
        for (float distance = start;
             distance + MarkingDashLength <= end;
             distance += MarkingDashLength + MarkingGapLength)
        {
            Vector2 first = PointAtDistance(samples, distances, distance);
            Vector2 second = PointAtDistance(
                samples,
                distances,
                distance + MarkingDashLength);
            Vector2 direction = (second - first).normalized;
            if (direction.sqrMagnitude < 0.5f)
            {
                continue;
            }

            Vector2 right = new Vector2(direction.y, -direction.x) * MarkingHalfWidth;
            Vector2 centre = PointAtDistance(
                samples,
                distances,
                distance + MarkingDashLength * 0.5f);
            Vector2 widthDirection = right.normalized;
            float roadWidth = MeasureHalfWidth(
                                  centre,
                                  -widthDirection,
                                  roadPlan,
                                  worldWidth,
                                  worldDepth,
                                  threshold) +
                              MeasureHalfWidth(
                                  centre,
                                  widthDirection,
                                  roadPlan,
                                  worldWidth,
                                  worldDepth,
                                  threshold);
            if (roadWidth < MarkingMinimumRoadWidth)
            {
                continue;
            }

            int firstVertex = vertices.Count;
            AddVertex(first - right, height, vertices, uvs);
            AddVertex(first + right, height, vertices, uvs);
            AddVertex(second + right, height, vertices, uvs);
            AddVertex(second - right, height, vertices, uvs);
            AddUpwardTriangle(
                firstVertex,
                firstVertex + 1,
                firstVertex + 2,
                vertices,
                triangles);
            AddUpwardTriangle(
                firstVertex,
                firstVertex + 2,
                firstVertex + 3,
                vertices,
                triangles);
            dashCount++;
        }

        return dashCount;
    }

    private static Vector2 PointAtDistance(
        List<Vector2> samples,
        float[] distances,
        float distance)
    {
        distance = Mathf.Clamp(distance, 0f, distances[distances.Length - 1]);
        int index = Array.BinarySearch(distances, distance);
        if (index >= 0)
        {
            return samples[index];
        }

        int next = Mathf.Clamp(~index, 1, samples.Count - 1);
        int previous = next - 1;
        float span = distances[next] - distances[previous];
        float blend = span <= Mathf.Epsilon
            ? 0f
            : (distance - distances[previous]) / span;
        return Vector2.Lerp(samples[previous], samples[next], blend);
    }

    private static List<Vector2> ConvertToWorldPoints(
        List<int> path,
        int imageWidth,
        int imageHeight,
        float worldWidth,
        float worldDepth)
    {
        List<Vector2> result = new List<Vector2>(path.Count);
        foreach (int index in path)
        {
            int x = index % imageWidth;
            int y = index / imageWidth;
            result.Add(new Vector2(
                worldWidth * ((float)x / (imageWidth - 1) - 0.5f),
                worldDepth * (0.5f - (float)y / (imageHeight - 1))));
        }

        return result;
    }

    private static float CalculateLength(List<Vector2> points)
    {
        float length = 0f;
        for (int index = 1; index < points.Count; index++)
        {
            length += Vector2.Distance(points[index - 1], points[index]);
        }

        return length;
    }

    private static List<Vector2> SampleSpline(
        List<Vector2> controls,
        bool closed,
        float spacing,
        Texture2D roadPlan,
        float worldWidth,
        float worldDepth,
        float threshold)
    {
        int uniqueCount = closed ? controls.Count - 1 : controls.Count;
        if (uniqueCount < 2)
        {
            return controls;
        }

        List<Vector2> result = new List<Vector2>();
        int segmentCount = closed ? uniqueCount : uniqueCount - 1;
        for (int segment = 0; segment < segmentCount; segment++)
        {
            int p1Index = segment;
            int p2Index = (segment + 1) % uniqueCount;
            int p0Index = closed
                ? (segment - 1 + uniqueCount) % uniqueCount
                : Mathf.Max(0, p1Index - 1);
            int p3Index = closed
                ? (segment + 2) % uniqueCount
                : Mathf.Min(uniqueCount - 1, p2Index + 1);
            Vector2 p0 = controls[p0Index];
            Vector2 p1 = controls[p1Index];
            Vector2 p2 = controls[p2Index];
            Vector2 p3 = controls[p3Index];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / spacing));

            for (int step = 0; step < steps; step++)
            {
                float t = (float)step / steps;
                Vector2 point = EvaluateCatmullRom(p0, p1, p2, p3, t);
                if (!IsInsideRoad(point, roadPlan, worldWidth, worldDepth, threshold))
                {
                    point = Vector2.Lerp(p1, p2, t);
                }

                if (result.Count == 0 || Vector2.Distance(result[result.Count - 1], point) > 0.05f)
                {
                    result.Add(point);
                }
            }
        }

        result.Add(closed ? result[0] : controls[controls.Count - 1]);
        return result;
    }

    private static Vector2 EvaluateCatmullRom(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static void AddRibbon(
        List<Vector2> samples,
        bool closed,
        bool capStart,
        bool capEnd,
        Texture2D roadPlan,
        float worldWidth,
        float worldDepth,
        float roadHeight,
        float threshold,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int count = samples.Count;
        Vector2[] rightVectors = new Vector2[count];
        float[] leftWidths = new float[count];
        float[] rightWidths = new float[count];

        for (int index = 0; index < count; index++)
        {
            int sampleIndex = closed && index == count - 1 ? 0 : index;
            int previous = Mathf.Max(0, sampleIndex - TangentSampleSpan);
            int next = Mathf.Min(count - 1, sampleIndex + TangentSampleSpan);
            if (closed)
            {
                int uniqueCount = count - 1;
                previous = (sampleIndex - TangentSampleSpan + uniqueCount) % uniqueCount;
                next = (sampleIndex + TangentSampleSpan) % uniqueCount;
            }

            Vector2 tangent = (samples[next] - samples[previous]).normalized;
            if (tangent.sqrMagnitude < 0.5f)
            {
                tangent = Vector2.right;
            }

            Vector2 right = new Vector2(tangent.y, -tangent.x);
            rightVectors[index] = right;
            leftWidths[index] = MeasureHalfWidth(
                samples[index], -right, roadPlan, worldWidth, worldDepth, threshold);
            rightWidths[index] = MeasureHalfWidth(
                samples[index], right, roadPlan, worldWidth, worldDepth, threshold);
        }

        ClampLocalWidthSpikes(leftWidths, closed);
        ClampLocalWidthSpikes(rightWidths, closed);

        if (!closed && !capStart)
        {
            StabilizeJunctionWidth(leftWidths, true);
            StabilizeJunctionWidth(rightWidths, true);
        }

        if (!closed && !capEnd)
        {
            StabilizeJunctionWidth(leftWidths, false);
            StabilizeJunctionWidth(rightWidths, false);
        }

        SmoothWidths(leftWidths, closed);
        SmoothWidths(rightWidths, closed);
        int firstVertex = vertices.Count;
        for (int index = 0; index < count; index++)
        {
            Vector2 left = samples[index] - rightVectors[index] * leftWidths[index];
            Vector2 right = samples[index] + rightVectors[index] * rightWidths[index];
            AddVertex(left, roadHeight, vertices, uvs);
            AddVertex(right, roadHeight, vertices, uvs);
            if (index == 0)
            {
                continue;
            }

            int previousLeft = firstVertex + (index - 1) * 2;
            int previousRight = previousLeft + 1;
            int currentLeft = firstVertex + index * 2;
            int currentRight = currentLeft + 1;
            AddUpwardTriangle(
                previousLeft,
                currentLeft,
                currentRight,
                vertices,
                triangles);
            AddUpwardTriangle(
                previousLeft,
                currentRight,
                previousRight,
                vertices,
                triangles);
        }

        if (!closed && capStart)
        {
            AddRoundEndCap(
                samples[0],
                (samples[0] - samples[1]).normalized,
                rightVectors[0],
                leftWidths[0],
                rightWidths[0],
                roadHeight,
                vertices,
                uvs,
                triangles);
        }

        if (!closed && capEnd)
        {
            int last = count - 1;
            AddRoundEndCap(
                samples[last],
                (samples[last] - samples[last - 1]).normalized,
                rightVectors[last],
                leftWidths[last],
                rightWidths[last],
                roadHeight,
                vertices,
                uvs,
                triangles);
        }
    }

    private static float MeasureHalfWidth(
        Vector2 centre,
        Vector2 direction,
        Texture2D roadPlan,
        float worldWidth,
        float worldDepth,
        float threshold)
    {
        float lastInside = 0f;
        for (float distance = WidthProbeStep;
             distance <= MaximumHalfWidth;
             distance += WidthProbeStep)
        {
            Vector2 point = centre + direction * distance;
            if (!IsInsideRoad(point, roadPlan, worldWidth, worldDepth, threshold))
            {
                break;
            }

            lastInside = distance;
        }

        return Mathf.Clamp(
            lastInside + WidthProbeStep * 0.5f,
            MinimumHalfWidth,
            MaximumHalfWidth);
    }

    private static bool IsInsideRoad(
        Vector2 point,
        Texture2D roadPlan,
        float worldWidth,
        float worldDepth,
        float threshold)
    {
        float u = point.x / worldWidth + 0.5f;
        float v = point.y / worldDepth + 0.5f;
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f &&
               roadPlan.GetPixelBilinear(u, v).grayscale > threshold;
    }

    private static void SmoothWidths(float[] widths, bool closed)
    {
        float[] source = (float[])widths.Clone();
        int uniqueCount = closed ? widths.Length - 1 : widths.Length;
        for (int index = 0; index < uniqueCount; index++)
        {
            float total = 0f;
            float weightTotal = 0f;
            for (int offset = -WidthSmoothRadius; offset <= WidthSmoothRadius; offset++)
            {
                int sample = index + offset;
                if (closed)
                {
                    sample = (sample % uniqueCount + uniqueCount) % uniqueCount;
                }
                else if (sample < 0 || sample >= uniqueCount)
                {
                    continue;
                }

                float weight = WidthSmoothRadius + 1 - Mathf.Abs(offset);
                total += source[sample] * weight;
                weightTotal += weight;
            }

            widths[index] = total / weightTotal;
        }

        if (closed)
        {
            widths[widths.Length - 1] = widths[0];
        }
    }

    private static void StabilizeJunctionWidth(float[] widths, bool atStart)
    {
        if (widths.Length < 3)
        {
            return;
        }

        int blendCount = Mathf.Min(JunctionWidthBlendSamples, widths.Length - 1);
        List<float> references = new List<float>(widths.Length);
        for (int index = 0; index < widths.Length; index++)
        {
            references.Add(widths[index]);
        }

        references.Sort();
        float stableWidth = references[references.Count / 2];
        for (int offset = 0; offset <= blendCount; offset++)
        {
            int index = atStart ? offset : widths.Length - 1 - offset;
            float blend = (float)offset / blendCount;
            float bounded = Mathf.Min(widths[index], stableWidth * 1.08f);
            widths[index] = Mathf.Lerp(stableWidth, bounded, blend);
        }
    }

    private static void ClampLocalWidthSpikes(float[] widths, bool closed)
    {
        if (widths.Length < 3)
        {
            return;
        }

        float[] source = (float[])widths.Clone();
        int uniqueCount = closed ? widths.Length - 1 : widths.Length;
        for (int index = 0; index < uniqueCount; index++)
        {
            List<float> neighbours = new List<float>(WidthSpikeWindow * 2 + 1);
            for (int offset = -WidthSpikeWindow; offset <= WidthSpikeWindow; offset++)
            {
                int sample = index + offset;
                if (closed)
                {
                    sample = (sample % uniqueCount + uniqueCount) % uniqueCount;
                }
                else if (sample < 0 || sample >= uniqueCount)
                {
                    continue;
                }

                neighbours.Add(source[sample]);
            }

            neighbours.Sort();
            float median = neighbours[neighbours.Count / 2];
            widths[index] = Mathf.Min(source[index], median * MaximumLocalWidthRatio);
        }

        if (closed)
        {
            widths[widths.Length - 1] = widths[0];
        }
    }

    private static void AddRoundEndCap(
        Vector2 centre,
        Vector2 outward,
        Vector2 right,
        float leftWidth,
        float rightWidth,
        float roadHeight,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        float radius = (leftWidth + rightWidth) * 0.5f;
        Vector2 capCentre = centre + right * (rightWidth - leftWidth) * 0.5f;
        int centreIndex = vertices.Count;
        AddVertex(capCentre, roadHeight, vertices, uvs);
        for (int segment = 0; segment <= CapSegments; segment++)
        {
            float angle = -Mathf.PI * 0.5f + Mathf.PI * segment / CapSegments;
            Vector2 point = capCentre +
                (outward * Mathf.Cos(angle) + right * Mathf.Sin(angle)) * radius;
            AddVertex(point, roadHeight, vertices, uvs);
            if (segment > 0)
            {
                AddUpwardTriangle(
                    centreIndex,
                    centreIndex + segment + 1,
                    centreIndex + segment,
                    vertices,
                    triangles);
            }
        }
    }

    private static void AddUpwardTriangle(
        int first,
        int second,
        int third,
        List<Vector3> vertices,
        List<int> triangles)
    {
        Vector3 normal = Vector3.Cross(
            vertices[second] - vertices[first],
            vertices[third] - vertices[first]);
        triangles.Add(first);
        if (normal.y >= 0f)
        {
            triangles.Add(second);
            triangles.Add(third);
        }
        else
        {
            triangles.Add(third);
            triangles.Add(second);
        }
    }

    private static void AddVertex(
        Vector2 point,
        float roadHeight,
        List<Vector3> vertices,
        List<Vector2> uvs)
    {
        vertices.Add(new Vector3(point.x, roadHeight, point.y));
        uvs.Add(new Vector2(point.x / 24f, point.y / 24f));
    }

    private static List<Vector2> Simplify(List<Vector2> points, float tolerance)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        bool[] keep = new bool[points.Count];
        keep[0] = true;
        keep[points.Count - 1] = true;
        SimplifySection(points, 0, points.Count - 1, tolerance * tolerance, keep);
        List<Vector2> result = new List<Vector2>();
        for (int index = 0; index < points.Count; index++)
        {
            if (keep[index])
            {
                result.Add(points[index]);
            }
        }

        return result;
    }

    private static List<Vector2> SmoothControls(
        List<Vector2> points,
        bool closed,
        Texture2D roadPlan,
        float worldWidth,
        float worldDepth,
        float threshold)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        List<Vector2> result = new List<Vector2>(points);
        int uniqueCount = closed ? points.Count - 1 : points.Count;
        for (int pass = 0; pass < ControlSmoothPasses; pass++)
        {
            Vector2[] source = result.ToArray();
            for (int index = 0; index < uniqueCount; index++)
            {
                if (!closed && (index == 0 || index == uniqueCount - 1))
                {
                    continue;
                }

                Vector2 total = Vector2.zero;
                float weightTotal = 0f;
                for (int offset = -ControlSmoothRadius;
                     offset <= ControlSmoothRadius;
                     offset++)
                {
                    int sample = index + offset;
                    if (closed)
                    {
                        sample = (sample % uniqueCount + uniqueCount) % uniqueCount;
                    }
                    else if (sample < 0 || sample >= uniqueCount)
                    {
                        continue;
                    }

                    float weight = ControlSmoothRadius + 1 - Mathf.Abs(offset);
                    total += source[sample] * weight;
                    weightTotal += weight;
                }

                Vector2 candidate = total / weightTotal;
                if (IsInsideRoad(
                    candidate,
                    roadPlan,
                    worldWidth,
                    worldDepth,
                    threshold))
                {
                    result[index] = candidate;
                }
            }

            if (closed)
            {
                result[result.Count - 1] = result[0];
            }
        }

        return result;
    }

    private static void SimplifySection(
        List<Vector2> points,
        int first,
        int last,
        float toleranceSquared,
        bool[] keep)
    {
        if (last <= first + 1)
        {
            return;
        }

        float maximumDistance = 0f;
        int maximumIndex = -1;
        for (int index = first + 1; index < last; index++)
        {
            float distance = DistanceToSegmentSquared(points[index], points[first], points[last]);
            if (distance > maximumDistance)
            {
                maximumDistance = distance;
                maximumIndex = index;
            }
        }

        if (maximumIndex < 0 || maximumDistance <= toleranceSquared)
        {
            return;
        }

        keep[maximumIndex] = true;
        SimplifySection(points, first, maximumIndex, toleranceSquared, keep);
        SimplifySection(points, maximumIndex, last, toleranceSquared, keep);
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        if (segment.sqrMagnitude <= Mathf.Epsilon)
        {
            return (point - start).sqrMagnitude;
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
        return (point - (start + segment * t)).sqrMagnitude;
    }

    private sealed class SkeletonGraph
    {
        private readonly bool[] skeleton;
        private readonly List<int> activeIndices = new List<int>();

        public SkeletonGraph(Texture2D roadPlan, float threshold)
        {
            Width = roadPlan.width;
            Height = roadPlan.height;
            skeleton = BuildMask(roadPlan, threshold);
            ThinSkeleton(skeleton, Width, Height);
            for (int index = 0; index < skeleton.Length; index++)
            {
                if (skeleton[index])
                {
                    activeIndices.Add(index);
                }
            }

            int[] pixelsByQuarter = new int[4];
            foreach (int index in activeIndices)
            {
                int y = index / Width;
                pixelsByQuarter[Mathf.Clamp(y * 4 / Height, 0, 3)]++;
            }
            Debug.Log(
                $"[Level03 Active-Plan Spline Roads] Skeleton pixels: {activeIndices.Count}; " +
                $"by image quarter: {string.Join(",", pixelsByQuarter)}.");
        }

        public int Width { get; }
        public int Height { get; }

        public bool IsTerminal(int index)
        {
            List<int> neighbours = new List<int>(8);
            GetNeighbours(index, neighbours);
            return neighbours.Count <= 1;
        }

        public List<List<int>> TracePaths()
        {
            List<List<int>> paths = new List<List<int>>();
            HashSet<ulong> visited = new HashSet<ulong>();
            List<int> neighbours = new List<int>(8);
            foreach (int start in activeIndices)
            {
                GetNeighbours(start, neighbours);
                if (neighbours.Count == 2)
                {
                    continue;
                }

                foreach (int neighbour in neighbours)
                {
                    if (!visited.Contains(EdgeKey(start, neighbour)))
                    {
                        List<int> path = Trace(start, neighbour, visited);
                        if (path.Count >= 2)
                        {
                            paths.Add(path);
                        }
                    }
                }
            }

            foreach (int start in activeIndices)
            {
                GetNeighbours(start, neighbours);
                foreach (int neighbour in neighbours)
                {
                    if (!visited.Contains(EdgeKey(start, neighbour)))
                    {
                        List<int> loop = Trace(start, neighbour, visited);
                        if (loop.Count >= 3)
                        {
                            paths.Add(loop);
                        }
                    }
                }
            }

            return paths;
        }

        private static bool[] BuildMask(Texture2D roadPlan, float threshold)
        {
            int width = roadPlan.width;
            int height = roadPlan.height;
            Color32[] pixels = roadPlan.GetPixels32();
            bool[] result = new bool[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    Color32 color = pixels[sourceRow * width + x];
                    float luminance = (color.r + color.g + color.b) / (3f * 255f);
                    result[y * width + x] = luminance > threshold;
                }
            }

            return result;
        }

        private static void ThinSkeleton(bool[] mask, int width, int height)
        {
            List<int> remove = new List<int>();
            bool changed;
            int iterations = 0;
            do
            {
                changed = false;
                for (int phase = 0; phase < 2; phase++)
                {
                    remove.Clear();
                    for (int y = 1; y < height - 1; y++)
                    {
                        for (int x = 1; x < width - 1; x++)
                        {
                            int index = y * width + x;
                            if (!mask[index] || !CanRemove(mask, width, index, phase))
                            {
                                continue;
                            }

                            remove.Add(index);
                        }
                    }

                    foreach (int index in remove)
                    {
                        mask[index] = false;
                    }

                    changed |= remove.Count > 0;
                }

                iterations++;
            }
            while (changed && iterations < 128);
        }

        private static bool CanRemove(bool[] mask, int width, int index, int phase)
        {
            bool p2 = mask[index - width];
            bool p3 = mask[index - width + 1];
            bool p4 = mask[index + 1];
            bool p5 = mask[index + width + 1];
            bool p6 = mask[index + width];
            bool p7 = mask[index + width - 1];
            bool p8 = mask[index - 1];
            bool p9 = mask[index - width - 1];
            bool[] neighbours = { p2, p3, p4, p5, p6, p7, p8, p9, p2 };
            int count = 0;
            int transitions = 0;
            for (int neighbour = 0; neighbour < 8; neighbour++)
            {
                if (neighbours[neighbour])
                {
                    count++;
                }

                if (!neighbours[neighbour] && neighbours[neighbour + 1])
                {
                    transitions++;
                }
            }

            if (count < 2 || count > 6 || transitions != 1)
            {
                return false;
            }

            return phase == 0
                ? !(p2 && p4 && p6) && !(p4 && p6 && p8)
                : !(p2 && p4 && p8) && !(p2 && p6 && p8);
        }

        private List<int> Trace(int start, int next, HashSet<ulong> visited)
        {
            List<int> path = new List<int> { start };
            int previous = start;
            int current = next;
            visited.Add(EdgeKey(previous, current));
            path.Add(current);
            List<int> neighbours = new List<int>(8);
            while (current != start)
            {
                GetNeighbours(current, neighbours);
                if (neighbours.Count != 2)
                {
                    break;
                }

                int candidate = neighbours[0] == previous ? neighbours[1] : neighbours[0];
                ulong edge = EdgeKey(current, candidate);
                if (visited.Contains(edge))
                {
                    if (candidate == start)
                    {
                        path.Add(start);
                    }
                    break;
                }

                visited.Add(edge);
                previous = current;
                current = candidate;
                path.Add(current);
            }

            return path;
        }

        private void GetNeighbours(int index, List<int> result)
        {
            result.Clear();
            int x = index % Width;
            int y = index / Width;
            for (int offset = 0; offset < NeighbourX.Length; offset++)
            {
                int dx = NeighbourX[offset];
                int dy = NeighbourY[offset];
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                {
                    continue;
                }

                int neighbour = ny * Width + nx;
                if (!skeleton[neighbour])
                {
                    continue;
                }

                // A diagonal beside an orthogonal skeleton pixel is only a
                // shortcut across the same one-pixel curve. Keeping it would
                // turn every stair-step bend into a false three-way junction.
                if (dx != 0 && dy != 0)
                {
                    int horizontal = y * Width + nx;
                    int vertical = ny * Width + x;
                    if (skeleton[horizontal] || skeleton[vertical])
                    {
                        continue;
                    }
                }

                result.Add(neighbour);
            }
        }

        private static ulong EdgeKey(int first, int second)
        {
            uint minimum = (uint)Mathf.Min(first, second);
            uint maximum = (uint)Mathf.Max(first, second);
            return ((ulong)minimum << 32) | maximum;
        }
    }
}

public static class Level03ActivePlanSplineRoadRebuilder
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string HeightReferencePath = "Assets/Level03/References/Level03_HeightReference.png";
    private const string RoadPlanPath = "Assets/Level03/References/Level03_RoadPlan_Active.png";
    private const string RoadMeshPath = "Assets/Level03/GeneratedTerrainRoad/MESH_Level03_Roads.asset";
    private const string RoadMarkingMeshPath =
        "Assets/Level03/GeneratedTerrainRoad/MESH_Level03_RoadMarkings.asset";
    private const string RoadMarkingMaterialPath =
        "Assets/Art/Materials/RoadMarkings/MAT_RoadMarking_White.mat";
    private const string RoadObjectName = "ENV_Level03_RoadNetwork_FromReference";
    private const string RoadMarkingObjectName = "ENV_Level03_RoadMarkings";
    private const float LandWidth = 4000f;
    private const float RoadHeight = 0.62f;
    private const float RoadThreshold = 0.55f;

    public static void RebuildFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Rebuild();
        RenderVerificationPreview();
    }

    [MenuItem("Tools/Island Map/Level03/Rebuild Active-Plan Spline Roads")]
    public static void Rebuild()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        Texture2D heightReference = LoadTexture(HeightReferencePath);
        Texture2D roadPlan = LoadTexture(RoadPlanPath);
        try
        {
            if (heightReference == null || roadPlan == null)
            {
                throw new InvalidOperationException(
                    "The Level03 height reference or active road plan is missing.");
            }

            float worldDepth = LandWidth * heightReference.height / heightReference.width;
            Mesh generated = Level03ActivePlanSplineRoadGenerator.Build(
                roadPlan,
                LandWidth,
                worldDepth,
                RoadHeight,
                RoadThreshold);
            Mesh generatedMarkings = Level03ActivePlanSplineRoadGenerator.BuildMarkings(
                roadPlan,
                LandWidth,
                worldDepth,
                RoadHeight,
                RoadThreshold);
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(RoadMeshPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(generated, RoadMeshPath);
                saved = generated;
            }
            else
            {
                EditorUtility.CopySerialized(generated, saved);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(saved);
            }

            Mesh savedMarkings = AssetDatabase.LoadAssetAtPath<Mesh>(RoadMarkingMeshPath);
            if (savedMarkings == null)
            {
                AssetDatabase.CreateAsset(generatedMarkings, RoadMarkingMeshPath);
                savedMarkings = generatedMarkings;
            }
            else
            {
                EditorUtility.CopySerialized(generatedMarkings, savedMarkings);
                UnityEngine.Object.DestroyImmediate(generatedMarkings);
                EditorUtility.SetDirty(savedMarkings);
            }

            GameObject roadObject = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.gameObject.name != RoadObjectName)
                    {
                        continue;
                    }

                    roadObject = filter.gameObject;
                    filter.sharedMesh = saved;
                    MeshCollider collider = filter.GetComponent<MeshCollider>();
                    if (collider != null)
                    {
                        collider.sharedMesh = null;
                        collider.sharedMesh = saved;
                    }
                }
            }

            if (roadObject == null)
            {
                throw new InvalidOperationException("The Level03 road object is missing.");
            }

            GameObject markingObject = GameObject.Find(RoadMarkingObjectName);
            if (markingObject == null)
            {
                markingObject = new GameObject(RoadMarkingObjectName);
                markingObject.transform.SetParent(roadObject.transform.parent, false);
                markingObject.AddComponent<MeshFilter>();
                markingObject.AddComponent<MeshRenderer>();
            }

            markingObject.layer = roadObject.layer;
            markingObject.isStatic = true;
            markingObject.GetComponent<MeshFilter>().sharedMesh = savedMarkings;
            markingObject.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(RoadMarkingMaterialPath);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = saved;
            Debug.Log(
                $"[Level03 Active-Plan Spline Roads] Rebuilt matching spline mesh with " +
                $"{saved.vertexCount:N0} vertices and {saved.triangles.Length / 3:N0} triangles.");
        }
        finally
        {
            DestroyTexture(heightReference);
            DestroyTexture(roadPlan);
        }
    }

    public static void RenderVerificationPreview()
    {
        Camera camera = null;
        foreach (Camera candidate in UnityEngine.Object.FindObjectsOfType<Camera>(true))
        {
            if (candidate.gameObject.name == "SYS_Level03_OverviewCamera")
            {
                camera = candidate;
                break;
            }
        }

        if (camera == null)
        {
            return;
        }

        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        bool originalOrthographic = camera.orthographic;
        float originalOrthographicSize = camera.orthographicSize;
        float originalFieldOfView = camera.fieldOfView;
        try
        {
            RenderCameraToPng(
                camera,
                1600,
                1300,
                "CodexLevel03ActivePlanSplinePreview.png");

            // The three authored connection types that previously produced visible
            // circular bulges: west bridge, north bridge, and south Y junction.
            RenderRoadDetail(camera, new Vector2(-430f, 190f), 250f,
                "CodexLevel03RoadDetail_WestBridge.png");
            RenderRoadDetail(camera, new Vector2(1280f, 1400f), 250f,
                "CodexLevel03RoadDetail_NorthBridge.png");
            RenderRoadDetail(camera, new Vector2(-230f, -1050f), 280f,
                "CodexLevel03RoadDetail_SouthJunction.png");

            camera.orthographic = false;
            camera.fieldOfView = 52f;
            camera.transform.position = new Vector3(2050f, 1350f, -2250f);
            camera.transform.LookAt(new Vector3(0f, 100f, 30f));
            RenderCameraToPng(
                camera,
                1600,
                900,
                "CodexLevel03PerspectivePreview.png");
        }
        finally
        {
            camera.transform.position = originalPosition;
            camera.transform.rotation = originalRotation;
            camera.orthographic = originalOrthographic;
            camera.orthographicSize = originalOrthographicSize;
            camera.fieldOfView = originalFieldOfView;
        }
    }

    private static void RenderRoadDetail(
        Camera camera,
        Vector2 center,
        float orthographicSize,
        string fileName)
    {
        camera.orthographic = true;
        camera.transform.position = new Vector3(
            center.x,
            camera.transform.position.y,
            center.y);
        camera.orthographicSize = orthographicSize;
        RenderCameraToPng(camera, 640, 640, fileName);
    }

    private static void RenderCameraToPng(
        Camera camera,
        int width,
        int height,
        string fileName)
    {
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllBytes(
                Path.Combine(projectRoot, "Library", fileName),
                image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static Texture2D LoadTexture(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false, true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        return texture.LoadImage(File.ReadAllBytes(assetPath), false) ? texture : null;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
