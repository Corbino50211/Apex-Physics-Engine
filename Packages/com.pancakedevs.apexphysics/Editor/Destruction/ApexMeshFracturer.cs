using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal readonly struct ApexFractureOptions
    {
        public ApexFractureOptions(
            int chunkCount,
            int seed,
            float minimumChunkVolume,
            float planeJitter,
            float capUvScale)
        {
            ChunkCount = Mathf.Clamp(chunkCount, 2, 64);
            Seed = seed;
            MinimumChunkVolume = Mathf.Max(0f, minimumChunkVolume);
            PlaneJitter = Mathf.Clamp01(planeJitter);
            CapUvScale = Mathf.Max(0.001f, capUvScale);
        }

        public int ChunkCount { get; }
        public int Seed { get; }
        public float MinimumChunkVolume { get; }
        public float PlaneJitter { get; }
        public float CapUvScale { get; }
    }

    internal static class ApexMeshFracturer
    {
        private const float PlaneEpsilon = 0.00001f;
        private const float LoopTolerance = 0.0005f;
        private const int SplitAttemptsPerChunk = 18;

        private readonly struct FractureVertex
        {
            public FractureVertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                Position = position;
                Normal = normal;
                UV = uv;
            }

            public Vector3 Position { get; }
            public Vector3 Normal { get; }
            public Vector2 UV { get; }

            public static FractureVertex Lerp(
                FractureVertex first,
                FractureVertex second,
                float t)
            {
                Vector3 normal = Vector3.Lerp(first.Normal, second.Normal, t);
                if (normal.sqrMagnitude > 0.000001f)
                {
                    normal.Normalize();
                }

                return new FractureVertex(
                    Vector3.Lerp(first.Position, second.Position, t),
                    normal,
                    Vector2.Lerp(first.UV, second.UV, t));
            }
        }

        private readonly struct FractureTriangle
        {
            public FractureTriangle(
                FractureVertex first,
                FractureVertex second,
                FractureVertex third,
                int submesh)
            {
                First = first;
                Second = second;
                Third = third;
                Submesh = submesh;
            }

            public FractureVertex First { get; }
            public FractureVertex Second { get; }
            public FractureVertex Third { get; }
            public int Submesh { get; }
        }

        private readonly struct CutSegment
        {
            public CutSegment(Vector3 first, Vector3 second)
            {
                First = first;
                Second = second;
            }

            public Vector3 First { get; }
            public Vector3 Second { get; }
        }

        private sealed class FracturePiece
        {
            public FracturePiece(List<FractureTriangle> triangles, int surfaceSubmeshCount)
            {
                Triangles = triangles;
                SurfaceSubmeshCount = surfaceSubmeshCount;
                RecalculateBounds();
            }

            public List<FractureTriangle> Triangles { get; }
            public int SurfaceSubmeshCount { get; }
            public Bounds Bounds { get; private set; }
            public float BoundsVolume => Bounds.size.x * Bounds.size.y * Bounds.size.z;

            public void RecalculateBounds()
            {
                if (Triangles == null || Triangles.Count == 0)
                {
                    Bounds = new Bounds(Vector3.zero, Vector3.zero);
                    return;
                }

                Bounds bounds = new Bounds(Triangles[0].First.Position, Vector3.zero);
                for (int i = 0; i < Triangles.Count; i++)
                {
                    FractureTriangle triangle = Triangles[i];
                    bounds.Encapsulate(triangle.First.Position);
                    bounds.Encapsulate(triangle.Second.Position);
                    bounds.Encapsulate(triangle.Third.Position);
                }

                Bounds = bounds;
            }

            public Mesh BuildMesh(string meshName)
            {
                int submeshCount = Mathf.Max(1, SurfaceSubmeshCount + 1);
                List<Vector3> vertices = new List<Vector3>(Triangles.Count * 3);
                List<Vector3> normals = new List<Vector3>(Triangles.Count * 3);
                List<Vector2> uvs = new List<Vector2>(Triangles.Count * 3);
                List<int>[] indices = new List<int>[submeshCount];
                for (int i = 0; i < submeshCount; i++)
                {
                    indices[i] = new List<int>();
                }

                for (int i = 0; i < Triangles.Count; i++)
                {
                    FractureTriangle triangle = Triangles[i];
                    int firstIndex = vertices.Count;
                    AddVertex(triangle.First, vertices, normals, uvs);
                    AddVertex(triangle.Second, vertices, normals, uvs);
                    AddVertex(triangle.Third, vertices, normals, uvs);

                    int submesh = Mathf.Clamp(triangle.Submesh, 0, submeshCount - 1);
                    indices[submesh].Add(firstIndex);
                    indices[submesh].Add(firstIndex + 1);
                    indices[submesh].Add(firstIndex + 2);
                }

                Mesh mesh = new Mesh
                {
                    name = meshName,
                    indexFormat = vertices.Count > 65535
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16,
                    subMeshCount = submeshCount
                };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                for (int i = 0; i < submeshCount; i++)
                {
                    mesh.SetTriangles(indices[i], i, false);
                }
                mesh.RecalculateBounds();
                return mesh;
            }

            private static void AddVertex(
                FractureVertex vertex,
                List<Vector3> vertices,
                List<Vector3> normals,
                List<Vector2> uvs)
            {
                vertices.Add(vertex.Position);
                normals.Add(vertex.Normal.sqrMagnitude > 0.000001f
                    ? vertex.Normal.normalized
                    : Vector3.up);
                uvs.Add(vertex.UV);
            }
        }

        public static bool TryFracture(
            Mesh sourceMesh,
            ApexFractureOptions options,
            out List<Mesh> meshes,
            out string error)
        {
            meshes = new List<Mesh>();
            error = string.Empty;

            if (sourceMesh == null)
            {
                error = "The selected object does not have a source mesh.";
                return false;
            }

            FracturePiece sourcePiece;
            try
            {
                sourcePiece = BuildSourcePiece(sourceMesh);
            }
            catch (Exception exception)
            {
                error =
                    "Apex could not read the source mesh. Enable Read/Write on the model importer and try again. " +
                    exception.Message;
                return false;
            }

            if (sourcePiece.Triangles.Count < 4)
            {
                error = "The source mesh does not contain enough triangles to fracture.";
                return false;
            }

            List<FracturePiece> pieces = new List<FracturePiece> { sourcePiece };
            System.Random random = new System.Random(options.Seed);
            int safety = options.ChunkCount * SplitAttemptsPerChunk * 2;

            while (pieces.Count < options.ChunkCount && safety-- > 0)
            {
                int pieceIndex = FindLargestSplittablePiece(pieces, options.MinimumChunkVolume);
                if (pieceIndex < 0)
                {
                    break;
                }

                FracturePiece piece = pieces[pieceIndex];
                bool splitSucceeded = false;
                for (int attempt = 0; attempt < SplitAttemptsPerChunk; attempt++)
                {
                    Plane plane = CreateSplitPlane(piece.Bounds, random, options.PlaneJitter);
                    if (!TrySplitPiece(
                            piece,
                            plane,
                            options.CapUvScale,
                            out FracturePiece positive,
                            out FracturePiece negative))
                    {
                        continue;
                    }

                    if (!IsUsablePiece(positive, options.MinimumChunkVolume) ||
                        !IsUsablePiece(negative, options.MinimumChunkVolume))
                    {
                        continue;
                    }

                    pieces.RemoveAt(pieceIndex);
                    pieces.Add(positive);
                    pieces.Add(negative);
                    splitSucceeded = true;
                    break;
                }

                if (!splitSucceeded)
                {
                    // Move the unsplittable piece to the front with a zero-volume marker by
                    // replacing it with itself. FindLargestSplittablePiece will eventually
                    // select another piece after the safety counter advances.
                    pieces.RemoveAt(pieceIndex);
                    pieces.Insert(0, piece);
                }
            }

            if (pieces.Count < 2)
            {
                error =
                    "Apex could not find a valid fracture plane. The mesh may be open, extremely thin, or non-manifold.";
                return false;
            }

            pieces.Sort((first, second) => second.BoundsVolume.CompareTo(first.BoundsVolume));
            for (int i = 0; i < pieces.Count; i++)
            {
                meshes.Add(pieces[i].BuildMesh(sourceMesh.name + "_ApexChunk_" + (i + 1)));
            }

            return true;
        }

        private static FracturePiece BuildSourcePiece(Mesh mesh)
        {
            Vector3[] positions = mesh.vertices;
            Vector3[] sourceNormals = mesh.normals;
            Vector2[] sourceUvs = mesh.uv;
            bool hasNormals = sourceNormals != null && sourceNormals.Length == positions.Length;
            bool hasUvs = sourceUvs != null && sourceUvs.Length == positions.Length;
            int surfaceSubmeshCount = Mathf.Max(1, mesh.subMeshCount);
            List<FractureTriangle> triangles = new List<FractureTriangle>();

            for (int submesh = 0; submesh < surfaceSubmeshCount; submesh++)
            {
                int[] indices = mesh.GetTriangles(submesh);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int firstIndex = indices[i];
                    int secondIndex = indices[i + 1];
                    int thirdIndex = indices[i + 2];
                    if (!IsValidVertexIndex(firstIndex, positions) ||
                        !IsValidVertexIndex(secondIndex, positions) ||
                        !IsValidVertexIndex(thirdIndex, positions))
                    {
                        continue;
                    }

                    Vector3 firstPosition = positions[firstIndex];
                    Vector3 secondPosition = positions[secondIndex];
                    Vector3 thirdPosition = positions[thirdIndex];
                    Vector3 faceNormal = Vector3.Cross(
                        secondPosition - firstPosition,
                        thirdPosition - firstPosition).normalized;
                    if (faceNormal.sqrMagnitude < 0.000001f)
                    {
                        continue;
                    }

                    FractureVertex first = new FractureVertex(
                        firstPosition,
                        hasNormals ? sourceNormals[firstIndex] : faceNormal,
                        hasUvs ? sourceUvs[firstIndex] : Vector2.zero);
                    FractureVertex second = new FractureVertex(
                        secondPosition,
                        hasNormals ? sourceNormals[secondIndex] : faceNormal,
                        hasUvs ? sourceUvs[secondIndex] : Vector2.zero);
                    FractureVertex third = new FractureVertex(
                        thirdPosition,
                        hasNormals ? sourceNormals[thirdIndex] : faceNormal,
                        hasUvs ? sourceUvs[thirdIndex] : Vector2.zero);
                    triangles.Add(new FractureTriangle(first, second, third, submesh));
                }
            }

            return new FracturePiece(triangles, surfaceSubmeshCount);
        }

        private static bool TrySplitPiece(
            FracturePiece source,
            Plane plane,
            float capUvScale,
            out FracturePiece positive,
            out FracturePiece negative)
        {
            List<FractureTriangle> positiveTriangles = new List<FractureTriangle>();
            List<FractureTriangle> negativeTriangles = new List<FractureTriangle>();
            List<CutSegment> cutSegments = new List<CutSegment>();

            for (int i = 0; i < source.Triangles.Count; i++)
            {
                FractureTriangle triangle = source.Triangles[i];
                FractureVertex[] polygon =
                {
                    triangle.First,
                    triangle.Second,
                    triangle.Third
                };

                float firstDistance = plane.GetDistanceToPoint(triangle.First.Position);
                float secondDistance = plane.GetDistanceToPoint(triangle.Second.Position);
                float thirdDistance = plane.GetDistanceToPoint(triangle.Third.Position);

                bool firstPositive = firstDistance > PlaneEpsilon;
                bool secondPositive = secondDistance > PlaneEpsilon;
                bool thirdPositive = thirdDistance > PlaneEpsilon;
                bool firstNegative = firstDistance < -PlaneEpsilon;
                bool secondNegative = secondDistance < -PlaneEpsilon;
                bool thirdNegative = thirdDistance < -PlaneEpsilon;

                if (!firstNegative && !secondNegative && !thirdNegative)
                {
                    positiveTriangles.Add(triangle);
                    continue;
                }

                if (!firstPositive && !secondPositive && !thirdPositive)
                {
                    negativeTriangles.Add(triangle);
                    continue;
                }

                List<FractureVertex> positivePolygon = ClipPolygon(polygon, plane, true);
                List<FractureVertex> negativePolygon = ClipPolygon(polygon, plane, false);
                AddPolygonTriangles(positivePolygon, triangle.Submesh, positiveTriangles);
                AddPolygonTriangles(negativePolygon, triangle.Submesh, negativeTriangles);

                if (TryBuildCutSegment(polygon, plane, out CutSegment segment))
                {
                    cutSegments.Add(segment);
                }
            }

            if (positiveTriangles.Count < 4 || negativeTriangles.Count < 4 || cutSegments.Count < 3)
            {
                positive = null;
                negative = null;
                return false;
            }

            int interiorSubmesh = source.SurfaceSubmeshCount;
            List<List<Vector3>> loops = BuildCutLoops(cutSegments);
            if (loops.Count == 0)
            {
                positive = null;
                negative = null;
                return false;
            }

            for (int i = 0; i < loops.Count; i++)
            {
                List<Vector3> loop = loops[i];
                AddCapTriangles(
                    loop,
                    -plane.normal,
                    interiorSubmesh,
                    capUvScale,
                    positiveTriangles);
                AddCapTriangles(
                    loop,
                    plane.normal,
                    interiorSubmesh,
                    capUvScale,
                    negativeTriangles);
            }

            positive = new FracturePiece(positiveTriangles, source.SurfaceSubmeshCount);
            negative = new FracturePiece(negativeTriangles, source.SurfaceSubmeshCount);
            return true;
        }

        private static List<FractureVertex> ClipPolygon(
            IReadOnlyList<FractureVertex> input,
            Plane plane,
            bool keepPositive)
        {
            List<FractureVertex> output = new List<FractureVertex>();
            if (input == null || input.Count == 0)
            {
                return output;
            }

            for (int i = 0; i < input.Count; i++)
            {
                FractureVertex current = input[i];
                FractureVertex next = input[(i + 1) % input.Count];
                float currentDistance = plane.GetDistanceToPoint(current.Position);
                float nextDistance = plane.GetDistanceToPoint(next.Position);
                bool currentInside = keepPositive
                    ? currentDistance >= -PlaneEpsilon
                    : currentDistance <= PlaneEpsilon;
                bool nextInside = keepPositive
                    ? nextDistance >= -PlaneEpsilon
                    : nextDistance <= PlaneEpsilon;

                if (currentInside)
                {
                    output.Add(current);
                }

                if (currentInside == nextInside)
                {
                    continue;
                }

                float denominator = currentDistance - nextDistance;
                if (Mathf.Abs(denominator) < PlaneEpsilon)
                {
                    continue;
                }

                float t = Mathf.Clamp01(currentDistance / denominator);
                output.Add(FractureVertex.Lerp(current, next, t));
            }

            RemoveDuplicatePolygonVertices(output);
            return output;
        }

        private static void AddPolygonTriangles(
            IReadOnlyList<FractureVertex> polygon,
            int submesh,
            List<FractureTriangle> triangles)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return;
            }

            FractureVertex origin = polygon[0];
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                triangles.Add(new FractureTriangle(
                    origin,
                    polygon[i],
                    polygon[i + 1],
                    submesh));
            }
        }

        private static bool TryBuildCutSegment(
            IReadOnlyList<FractureVertex> polygon,
            Plane plane,
            out CutSegment segment)
        {
            List<Vector3> intersections = new List<Vector3>(3);
            for (int i = 0; i < polygon.Count; i++)
            {
                FractureVertex current = polygon[i];
                FractureVertex next = polygon[(i + 1) % polygon.Count];
                float currentDistance = plane.GetDistanceToPoint(current.Position);
                float nextDistance = plane.GetDistanceToPoint(next.Position);

                if (Mathf.Abs(currentDistance) <= PlaneEpsilon)
                {
                    AddUniquePoint(intersections, current.Position, LoopTolerance);
                }

                if ((currentDistance > PlaneEpsilon && nextDistance < -PlaneEpsilon) ||
                    (currentDistance < -PlaneEpsilon && nextDistance > PlaneEpsilon))
                {
                    float t = currentDistance / (currentDistance - nextDistance);
                    AddUniquePoint(
                        intersections,
                        Vector3.Lerp(current.Position, next.Position, t),
                        LoopTolerance);
                }
            }

            if (intersections.Count < 2)
            {
                segment = default;
                return false;
            }

            int firstIndex = 0;
            int secondIndex = 1;
            float greatestDistance = 0f;
            for (int first = 0; first < intersections.Count; first++)
            {
                for (int second = first + 1; second < intersections.Count; second++)
                {
                    float distance = (intersections[first] - intersections[second]).sqrMagnitude;
                    if (distance > greatestDistance)
                    {
                        greatestDistance = distance;
                        firstIndex = first;
                        secondIndex = second;
                    }
                }
            }

            if (greatestDistance <= PlaneEpsilon * PlaneEpsilon)
            {
                segment = default;
                return false;
            }

            segment = new CutSegment(intersections[firstIndex], intersections[secondIndex]);
            return true;
        }

        private static List<List<Vector3>> BuildCutLoops(List<CutSegment> segments)
        {
            List<List<Vector3>> loops = new List<List<Vector3>>();
            bool[] used = new bool[segments.Count];

            for (int startSegment = 0; startSegment < segments.Count; startSegment++)
            {
                if (used[startSegment])
                {
                    continue;
                }

                CutSegment segment = segments[startSegment];
                used[startSegment] = true;
                List<Vector3> loop = new List<Vector3>
                {
                    segment.First,
                    segment.Second
                };

                Vector3 start = segment.First;
                Vector3 current = segment.Second;
                bool closed = false;

                for (int guard = 0; guard < segments.Count + 4; guard++)
                {
                    if (Approximately(current, start, LoopTolerance))
                    {
                        closed = true;
                        break;
                    }

                    int nextSegmentIndex = FindConnectingSegment(
                        segments,
                        used,
                        current,
                        out Vector3 nextPoint);
                    if (nextSegmentIndex < 0)
                    {
                        break;
                    }

                    used[nextSegmentIndex] = true;
                    current = nextPoint;
                    if (!Approximately(current, start, LoopTolerance))
                    {
                        loop.Add(current);
                    }
                }

                RemoveDuplicateLoopPoints(loop);
                if (closed && loop.Count >= 3)
                {
                    loops.Add(loop);
                }
            }

            return loops;
        }

        private static int FindConnectingSegment(
            IReadOnlyList<CutSegment> segments,
            IReadOnlyList<bool> used,
            Vector3 current,
            out Vector3 nextPoint)
        {
            int closestIndex = -1;
            float closestDistance = float.PositiveInfinity;
            nextPoint = current;

            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                CutSegment segment = segments[i];
                float firstDistance = (segment.First - current).sqrMagnitude;
                float secondDistance = (segment.Second - current).sqrMagnitude;
                float toleranceSquared = LoopTolerance * LoopTolerance;

                if (firstDistance <= toleranceSquared && firstDistance < closestDistance)
                {
                    closestDistance = firstDistance;
                    closestIndex = i;
                    nextPoint = segment.Second;
                }

                if (secondDistance <= toleranceSquared && secondDistance < closestDistance)
                {
                    closestDistance = secondDistance;
                    closestIndex = i;
                    nextPoint = segment.First;
                }
            }

            return closestIndex;
        }

        private static void AddCapTriangles(
            List<Vector3> sourceLoop,
            Vector3 desiredNormal,
            int submesh,
            float uvScale,
            List<FractureTriangle> triangles)
        {
            if (sourceLoop == null || sourceLoop.Count < 3)
            {
                return;
            }

            Vector3 normal = desiredNormal.normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;
            Vector3 axisU = Vector3.Cross(reference, normal).normalized;
            Vector3 axisV = Vector3.Cross(normal, axisU).normalized;

            List<Vector3> loop = new List<Vector3>(sourceLoop);
            List<Vector2> projected = ProjectLoop(loop, axisU, axisV);
            if (SignedArea(projected) < 0f)
            {
                loop.Reverse();
                projected.Reverse();
            }

            List<int> triangulated = EarClip(projected);
            if (triangulated.Count < 3)
            {
                triangulated = FanTriangulate(loop.Count);
            }

            for (int i = 0; i + 2 < triangulated.Count; i += 3)
            {
                int firstIndex = triangulated[i];
                int secondIndex = triangulated[i + 1];
                int thirdIndex = triangulated[i + 2];

                FractureVertex first = new FractureVertex(
                    loop[firstIndex],
                    normal,
                    projected[firstIndex] * uvScale);
                FractureVertex second = new FractureVertex(
                    loop[secondIndex],
                    normal,
                    projected[secondIndex] * uvScale);
                FractureVertex third = new FractureVertex(
                    loop[thirdIndex],
                    normal,
                    projected[thirdIndex] * uvScale);
                triangles.Add(new FractureTriangle(first, second, third, submesh));
            }
        }

        private static List<int> EarClip(IReadOnlyList<Vector2> points)
        {
            List<int> result = new List<int>();
            if (points == null || points.Count < 3)
            {
                return result;
            }

            List<int> remaining = new List<int>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                remaining.Add(i);
            }

            int safety = points.Count * points.Count;
            while (remaining.Count > 3 && safety-- > 0)
            {
                bool earFound = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int previous = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int current = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    Vector2 first = points[previous];
                    Vector2 second = points[current];
                    Vector2 third = points[next];

                    if (Cross(second - first, third - second) <= PlaneEpsilon)
                    {
                        continue;
                    }

                    bool containsPoint = false;
                    for (int other = 0; other < remaining.Count; other++)
                    {
                        int candidate = remaining[other];
                        if (candidate == previous || candidate == current || candidate == next)
                        {
                            continue;
                        }

                        if (PointInTriangle(points[candidate], first, second, third))
                        {
                            containsPoint = true;
                            break;
                        }
                    }

                    if (containsPoint)
                    {
                        continue;
                    }

                    result.Add(previous);
                    result.Add(current);
                    result.Add(next);
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    result.Clear();
                    return result;
                }
            }

            if (remaining.Count == 3)
            {
                result.Add(remaining[0]);
                result.Add(remaining[1]);
                result.Add(remaining[2]);
            }

            return result;
        }

        private static Plane CreateSplitPlane(
            Bounds bounds,
            System.Random random,
            float planeJitter)
        {
            Vector3 size = bounds.size;
            Vector3 primaryAxis;
            float primaryExtent;

            if (size.x >= size.y && size.x >= size.z)
            {
                primaryAxis = Vector3.right;
                primaryExtent = bounds.extents.x;
            }
            else if (size.y >= size.x && size.y >= size.z)
            {
                primaryAxis = Vector3.up;
                primaryExtent = bounds.extents.y;
            }
            else
            {
                primaryAxis = Vector3.forward;
                primaryExtent = bounds.extents.z;
            }

            Vector3 randomDirection = new Vector3(
                RandomRange(random, -1f, 1f),
                RandomRange(random, -1f, 1f),
                RandomRange(random, -1f, 1f));
            if (randomDirection.sqrMagnitude < 0.0001f)
            {
                randomDirection = Vector3.one;
            }
            randomDirection.Normalize();

            Vector3 normal = (primaryAxis + randomDirection * planeJitter * 0.65f).normalized;
            float offset = RandomRange(random, -0.22f, 0.22f) * primaryExtent;
            Vector3 point = bounds.center + primaryAxis * offset;
            return new Plane(normal, point);
        }

        private static int FindLargestSplittablePiece(
            IReadOnlyList<FracturePiece> pieces,
            float minimumVolume)
        {
            int index = -1;
            float greatestScore = float.NegativeInfinity;
            for (int i = 0; i < pieces.Count; i++)
            {
                FracturePiece piece = pieces[i];
                if (!IsUsablePiece(piece, minimumVolume * 2f))
                {
                    continue;
                }

                float score = piece.BoundsVolume + piece.Triangles.Count * 0.000001f;
                if (score > greatestScore)
                {
                    greatestScore = score;
                    index = i;
                }
            }

            return index;
        }

        private static bool IsUsablePiece(FracturePiece piece, float minimumVolume)
        {
            return piece != null &&
                   piece.Triangles != null &&
                   piece.Triangles.Count >= 4 &&
                   piece.BoundsVolume >= minimumVolume &&
                   piece.Bounds.size.sqrMagnitude > 0.000001f;
        }

        private static List<Vector2> ProjectLoop(
            IReadOnlyList<Vector3> loop,
            Vector3 axisU,
            Vector3 axisV)
        {
            List<Vector2> projected = new List<Vector2>(loop.Count);
            for (int i = 0; i < loop.Count; i++)
            {
                projected.Add(new Vector2(
                    Vector3.Dot(loop[i], axisU),
                    Vector3.Dot(loop[i], axisV)));
            }
            return projected;
        }

        private static float SignedArea(IReadOnlyList<Vector2> points)
        {
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        private static List<int> FanTriangulate(int pointCount)
        {
            List<int> result = new List<int>();
            for (int i = 1; i < pointCount - 1; i++)
            {
                result.Add(0);
                result.Add(i);
                result.Add(i + 1);
            }
            return result;
        }

        private static bool PointInTriangle(
            Vector2 point,
            Vector2 first,
            Vector2 second,
            Vector2 third)
        {
            float firstCross = Cross(second - first, point - first);
            float secondCross = Cross(third - second, point - second);
            float thirdCross = Cross(first - third, point - third);
            return firstCross >= -PlaneEpsilon &&
                   secondCross >= -PlaneEpsilon &&
                   thirdCross >= -PlaneEpsilon;
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        private static void RemoveDuplicatePolygonVertices(List<FractureVertex> polygon)
        {
            for (int i = polygon.Count - 1; i >= 0; i--)
            {
                int previous = (i - 1 + polygon.Count) % polygon.Count;
                if (polygon.Count > 1 && Approximately(
                        polygon[i].Position,
                        polygon[previous].Position,
                        LoopTolerance))
                {
                    polygon.RemoveAt(i);
                }
            }
        }

        private static void RemoveDuplicateLoopPoints(List<Vector3> loop)
        {
            for (int i = loop.Count - 1; i >= 0; i--)
            {
                int previous = (i - 1 + loop.Count) % loop.Count;
                if (loop.Count > 1 && Approximately(
                        loop[i],
                        loop[previous],
                        LoopTolerance))
                {
                    loop.RemoveAt(i);
                }
            }
        }

        private static void AddUniquePoint(
            List<Vector3> points,
            Vector3 point,
            float tolerance)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (Approximately(points[i], point, tolerance))
                {
                    return;
                }
            }
            points.Add(point);
        }

        private static bool Approximately(
            Vector3 first,
            Vector3 second,
            float tolerance)
        {
            return (first - second).sqrMagnitude <= tolerance * tolerance;
        }

        private static bool IsValidVertexIndex(int index, IReadOnlyList<Vector3> vertices)
        {
            return index >= 0 && index < vertices.Count;
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }
    }
}
