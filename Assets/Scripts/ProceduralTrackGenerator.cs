using System.Collections.Generic;
using UnityEngine;

public sealed class ProceduralTrackGenerator : MonoBehaviour
{
    private const int NodeCount = 48;
    private const float RoadY = 0.02f;
    private const float CurbY = 0.18f;
    private const float WallY = 0.75f;

    [SerializeField] private AStarGrid grid;
    [SerializeField] private RaceSettings settings;
    [SerializeField] private bool randomizeEveryRace = true;
    [SerializeField] private float trackRadius = 78f;
    [SerializeField] private float trackWidth = 28f;
    [SerializeField] private float curbWidth = 3.2f;
    [SerializeField] private float wallWidth = 1.4f;

    private readonly List<Transform> generatedNodes = new List<Transform>();
    private readonly List<Transform> parkingSpots = new List<Transform>();
    private Transform nodeRoot;
    private Transform parkingRoot;
    private GameObject generatedRoot;
    private Material roadMaterial;
    private Material curbRedMaterial;
    private Material curbWhiteMaterial;
    private Material wallMaterial;
    private Material startMaterial;
    private Material groundMaterial;
    private Vector3[] centerline;
    private int generationCounter;

    public static ProceduralTrackGenerator Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<ProceduralTrackGenerator>() != null)
        {
            return;
        }

        var sceneGrid = FindObjectOfType<AStarGrid>();
        if (sceneGrid == null)
        {
            return;
        }

        sceneGrid.gameObject.AddComponent<ProceduralTrackGenerator>();
    }

    private void Awake()
    {
        Instance = this;
        if (grid == null)
        {
            grid = FindObjectOfType<AStarGrid>();
        }

        if (settings == null)
        {
            settings = FindObjectOfType<RaceSettings>();
        }

        LoadMaterials();
        GenerateTrack();
    }

    public void GenerateTrack()
    {
        if (grid == null)
        {
            return;
        }

        DisableStaticTrackPieces();
        EnsureRoots();

        generationCounter++;
        var seed = randomizeEveryRace
            ? System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode() ^ (generationCounter * 73856093)
            : 20260512;
        centerline = BuildCenterline(seed);

        BuildGroundSurface();
        BuildRoadSurface();
        BuildCurbSurface();
        BuildCollisionWalls();
        BuildStartMarker();
        BuildNodes();
        BuildParkingSpots();
        PlaceCars();
    }

    private void LoadMaterials()
    {
        roadMaterial = LoadMaterial("Road", new Color(0.02f, 0.025f, 0.025f));
        curbRedMaterial = LoadMaterial("Curbs", Color.red);
        curbWhiteMaterial = LoadMaterial("CurbsWhite", Color.white);
        wallMaterial = LoadMaterial("Tires", new Color(0.02f, 0.02f, 0.02f));
        startMaterial = LoadMaterial("CarGlass", new Color(0.95f, 0.95f, 0.85f));
        groundMaterial = LoadMaterial("Infield", new Color(0.16f, 0.36f, 0.22f));
    }

    private static Material LoadMaterial(string materialName, Color fallbackColor)
    {
        foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (material != null && material.name == materialName)
            {
                return material;
            }
        }

        var fallbackMaterial = new Material(Shader.Find("Standard"));
        fallbackMaterial.color = fallbackColor;
        return fallbackMaterial;
    }

    private void DisableStaticTrackPieces()
    {
        DisableByName("Continuous Road Surface");
        DisableByName("ContinuousRoadMesh");
        DisableByName("Left Continuous Curb");
        DisableByName("Right Continuous Curb");
        DisableByName("Left Continuous Curb Mesh");
        DisableByName("Right Continuous Curb Mesh");
        DisableByName("Start Line");
        DisableByName("Start Left Post");
        DisableByName("Start Right Post");

        for (var i = 1; i <= 5; i++)
        {
            DisableByName($"Finish Parking Spot {i}");
        }

        for (var i = 0; i < 32; i++)
        {
            DisableByName($"AStar Node {i:00}");
        }
    }

    private static void DisableByName(string objectName)
    {
        var target = GameObject.Find(objectName);
        if (target != null && !target.name.StartsWith("Generated"))
        {
            target.SetActive(false);
        }
    }

    private void EnsureRoots()
    {
        if (generatedRoot != null)
        {
            generatedRoot.SetActive(false);
            Destroy(generatedRoot);
        }

        generatedRoot = new GameObject("Generated Runtime Track");
        nodeRoot = new GameObject("Generated AStar Nodes").transform;
        nodeRoot.SetParent(generatedRoot.transform, false);
        parkingRoot = new GameObject("Generated Finish Parking").transform;
        parkingRoot.SetParent(generatedRoot.transform, false);
        generatedNodes.Clear();
        parkingSpots.Clear();
    }

    private Vector3[] BuildCenterline(int seed)
    {
        var random = new System.Random(seed);
        var points = new Vector3[NodeCount];
        var trackType = random.Next(0, 3);
        var baseRadius = trackRadius * Mathf.Lerp(0.86f, 1.38f, (float)random.NextDouble());
        var radiusX = baseRadius;
        var radiusZ = baseRadius;
        var snakeAmplitude = 0f;
        var snakeFrequency = 2f;

        if (trackType == 1)
        {
            radiusX = baseRadius * Mathf.Lerp(1.45f, 1.95f, (float)random.NextDouble());
            radiusZ = baseRadius * Mathf.Lerp(0.68f, 0.86f, (float)random.NextDouble());
        }
        else if (trackType == 2)
        {
            radiusX = baseRadius * Mathf.Lerp(1.2f, 1.55f, (float)random.NextDouble());
            radiusZ = baseRadius * Mathf.Lerp(0.78f, 1.02f, (float)random.NextDouble());
            snakeAmplitude = baseRadius * Mathf.Lerp(0.22f, 0.34f, (float)random.NextDouble());
            snakeFrequency = random.NextDouble() > 0.5 ? 2f : 3f;
        }

        var rotation = Mathf.Lerp(-24f, 24f, (float)random.NextDouble()) * Mathf.Deg2Rad;
        var phase = Mathf.Lerp(0f, Mathf.PI * 2f, (float)random.NextDouble());

        for (var i = 0; i < NodeCount; i++)
        {
            var angle = Mathf.PI * 2f * i / NodeCount - Mathf.PI * 0.5f;
            var x = Mathf.Cos(angle) * radiusX;
            var z = Mathf.Sin(angle) * radiusZ;

            if (trackType == 2)
            {
                x += Mathf.Sin(angle * snakeFrequency + phase) * snakeAmplitude;
            }

            var rotatedX = x * Mathf.Cos(rotation) - z * Mathf.Sin(rotation);
            var rotatedZ = x * Mathf.Sin(rotation) + z * Mathf.Cos(rotation);
            points[i] = new Vector3(rotatedX, 0f, rotatedZ);
        }

        AlignStartToStraight(points);
        return points;
    }

    private static void AlignStartToStraight(Vector3[] points)
    {
        var midpoint = (points[0] + points[1]) * 0.5f;
        var shift = new Vector3(0f, 0f, -86f) - midpoint;

        for (var i = 0; i < points.Length; i++)
        {
            points[i] += shift;
        }
    }

    private void BuildGroundSurface()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Generated Large Ground";
        ground.transform.SetParent(generatedRoot.transform, false);
        ground.transform.position = new Vector3(0f, -0.16f, 0f);
        ground.transform.localScale = new Vector3(430f, 0.24f, 380f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
    }

    private void BuildRoadSurface()
    {
        var mesh = BuildLoopRibbonMesh(centerline, -trackWidth * 0.5f, trackWidth * 0.5f, RoadY, "Generated Continuous Road Mesh");
        var road = CreateMeshObject("Generated Continuous Road Surface", mesh, roadMaterial, true);
        road.layer = 0;
    }

    private void BuildCurbSurface()
    {
        var curbMesh = new Mesh { name = "Generated Continuous Curb Mesh" };
        var vertices = new List<Vector3>();
        var redTriangles = new List<int>();
        var whiteTriangles = new List<int>();

        AppendCurbSide(vertices, redTriangles, whiteTriangles, trackWidth * 0.5f, trackWidth * 0.5f + curbWidth);
        AppendCurbSide(vertices, redTriangles, whiteTriangles, -trackWidth * 0.5f - curbWidth, -trackWidth * 0.5f);

        curbMesh.SetVertices(vertices);
        curbMesh.subMeshCount = 2;
        curbMesh.SetTriangles(redTriangles, 0);
        curbMesh.SetTriangles(whiteTriangles, 1);
        curbMesh.RecalculateNormals();
        curbMesh.RecalculateBounds();

        var curb = CreateMeshObject("Generated Continuous Red White Curbs", curbMesh, null, true);
        curb.GetComponent<MeshRenderer>().sharedMaterials = new[] { curbRedMaterial, curbWhiteMaterial };
    }

    private void AppendCurbSide(List<Vector3> vertices, List<int> redTriangles, List<int> whiteTriangles, float innerOffset, float outerOffset)
    {
        for (var i = 0; i < centerline.Length; i++)
        {
            var nextIndex = (i + 1) % centerline.Length;
            var innerA = OffsetPoint(i, innerOffset);
            var outerA = OffsetPoint(i, outerOffset);
            var innerB = OffsetPoint(nextIndex, innerOffset);
            var outerB = OffsetPoint(nextIndex, outerOffset);

            var start = vertices.Count;
            vertices.Add(new Vector3(innerA.x, CurbY, innerA.z));
            vertices.Add(new Vector3(outerA.x, CurbY, outerA.z));
            vertices.Add(new Vector3(innerB.x, CurbY, innerB.z));
            vertices.Add(new Vector3(outerB.x, CurbY, outerB.z));

            var targetTriangles = i % 2 == 0 ? redTriangles : whiteTriangles;
            targetTriangles.Add(start);
            targetTriangles.Add(start + 2);
            targetTriangles.Add(start + 1);
            targetTriangles.Add(start + 1);
            targetTriangles.Add(start + 2);
            targetTriangles.Add(start + 3);
        }
    }

    private void BuildCollisionWalls()
    {
        CreateMeshObject("Generated Left Collision Wall", BuildWallMesh(trackWidth * 0.5f + curbWidth + wallWidth * 0.5f), wallMaterial, true);
        CreateMeshObject("Generated Right Collision Wall", BuildWallMesh(-trackWidth * 0.5f - curbWidth - wallWidth * 0.5f), wallMaterial, true);
    }

    private Mesh BuildWallMesh(float offset)
    {
        var mesh = new Mesh { name = "Generated Wall Mesh" };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (var i = 0; i < centerline.Length; i++)
        {
            var nextIndex = (i + 1) % centerline.Length;
            var bottomA = OffsetPoint(i, offset);
            var bottomB = OffsetPoint(nextIndex, offset);

            var start = vertices.Count;
            vertices.Add(new Vector3(bottomA.x, 0.05f, bottomA.z));
            vertices.Add(new Vector3(bottomB.x, 0.05f, bottomB.z));
            vertices.Add(new Vector3(bottomA.x, WallY, bottomA.z));
            vertices.Add(new Vector3(bottomB.x, WallY, bottomB.z));

            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Mesh BuildLoopRibbonMesh(IReadOnlyList<Vector3> points, float leftOffset, float rightOffset, float y, string meshName)
    {
        var mesh = new Mesh { name = meshName };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (var i = 0; i < points.Count; i++)
        {
            var left = OffsetPoint(i, leftOffset);
            var right = OffsetPoint(i, rightOffset);
            vertices.Add(new Vector3(left.x, y, left.z));
            vertices.Add(new Vector3(right.x, y, right.z));
        }

        for (var i = 0; i < points.Count; i++)
        {
            var nextIndex = (i + 1) % points.Count;
            var leftA = i * 2;
            var rightA = leftA + 1;
            var leftB = nextIndex * 2;
            var rightB = leftB + 1;

            triangles.Add(leftA);
            triangles.Add(leftB);
            triangles.Add(rightA);
            triangles.Add(rightA);
            triangles.Add(leftB);
            triangles.Add(rightB);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector3 OffsetPoint(int index, float offset)
    {
        var normal = GetNodeNormal(index);
        return centerline[index] + normal * offset;
    }

    private Vector3 GetNodeNormal(int index)
    {
        var count = centerline.Length;
        var previous = centerline[(index - 1 + count) % count];
        var next = centerline[(index + 1) % count];
        return GetSegmentNormal(previous, next);
    }

    private GameObject CreateMeshObject(string objectName, Mesh mesh, Material material, bool collider)
    {
        var target = new GameObject(objectName);
        target.transform.SetParent(generatedRoot.transform, false);
        var filter = target.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = target.AddComponent<MeshRenderer>();
        if (material != null)
        {
            renderer.sharedMaterial = material;
        }

        if (collider)
        {
            var meshCollider = target.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = false;
        }

        return target;
    }

    private void BuildStartMarker()
    {
        var start = centerline[0];
        var next = centerline[1];
        var direction = (next - start).normalized;
        var normal = GetSegmentNormal(start, next);
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Generated Start Finish Line";
        marker.transform.SetParent(generatedRoot.transform, false);
        marker.transform.position = start + direction * 7f + Vector3.up * 0.09f;
        marker.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        marker.transform.localScale = new Vector3(trackWidth + curbWidth * 1.8f, 0.12f, 2.4f);
        marker.GetComponent<MeshRenderer>().sharedMaterial = startMaterial;
        Destroy(marker.GetComponent<Collider>());

        var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPost.name = "Generated Start Left Post";
        leftPost.transform.SetParent(generatedRoot.transform, false);
        leftPost.transform.position = marker.transform.position + normal * (trackWidth * 0.5f + curbWidth + 1.5f) + Vector3.up * 2.4f;
        leftPost.transform.localScale = new Vector3(1.2f, 4.8f, 1.2f);
        leftPost.GetComponent<MeshRenderer>().sharedMaterial = curbRedMaterial;

        var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPost.name = "Generated Start Right Post";
        rightPost.transform.SetParent(generatedRoot.transform, false);
        rightPost.transform.position = marker.transform.position - normal * (trackWidth * 0.5f + curbWidth + 1.5f) + Vector3.up * 2.4f;
        rightPost.transform.localScale = new Vector3(1.2f, 4.8f, 1.2f);
        rightPost.GetComponent<MeshRenderer>().sharedMaterial = curbWhiteMaterial;
    }

    private void BuildNodes()
    {
        generatedNodes.Clear();
        for (var i = 0; i < centerline.Length; i++)
        {
            var node = new GameObject($"Generated AStar Node {i:00}").transform;
            node.SetParent(nodeRoot, false);
            node.position = centerline[i] + Vector3.up * 0.25f;
            generatedNodes.Add(node);
        }

        grid.SetNodes(generatedNodes);
    }

    private void BuildParkingSpots()
    {
        parkingSpots.Clear();
        var start = centerline[0];
        var next = centerline[1];
        var direction = (next - start).normalized;
        var normal = GetSegmentNormal(start, next);
        var parkingBase = start - direction * 22f - normal * (trackWidth * 0.5f + curbWidth + 16f);

        for (var i = 0; i < 5; i++)
        {
            var spot = new GameObject($"Generated Finish Parking Spot {i + 1}").transform;
            spot.SetParent(parkingRoot, false);
            spot.position = parkingBase - direction * (i * 7f);
            spot.rotation = Quaternion.LookRotation(direction, Vector3.up);
            parkingSpots.Add(spot);
        }
    }

    private void PlaceCars()
    {
        var player = FindObjectOfType<PlayerCarController>();
        var opponents = FindObjectsOfType<RacingAIDriver>(true);
        var trackers = FindObjectsOfType<RaceProgressTracker>(true);

        foreach (var tracker in trackers)
        {
            tracker.Configure(grid, settings, tracker.RacerName);
            tracker.ResetRace();
        }

        var start = centerline[0];
        var next = centerline[1];
        var direction = (next - start).normalized;
        var normal = GetSegmentNormal(start, next);
        var rotation = Quaternion.LookRotation(direction, Vector3.up);

        PlaceRigidbody(player != null ? player.GetComponent<Rigidbody>() : null, start + direction * 3f - normal * 3.5f + Vector3.up * 1f, rotation);

        for (var i = 0; i < opponents.Length; i++)
        {
            var row = i / 2;
            var side = i % 2 == 0 ? 1f : -1f;
            var position = start - direction * (7f + row * 8f) + normal * (side * 4f) + Vector3.up * 1f;
            PlaceRigidbody(opponents[i].GetComponent<Rigidbody>(), position, rotation);

            var laneOffset = Mathf.Lerp(-trackWidth * 0.22f, trackWidth * 0.22f, i / 4f);
            var aiSpeed = 27.5f + i * 0.65f;
            var parkingSpot = parkingSpots.Count > i ? parkingSpots[i] : null;
            opponents[i].Configure(grid, Mathf.Min(1 + i, generatedNodes.Count - 1), laneOffset, aiSpeed, parkingSpot);
        }
    }

    private static void PlaceRigidbody(Rigidbody body, Vector3 position, Quaternion rotation)
    {
        if (body == null)
        {
            return;
        }

        body.position = position;
        body.rotation = rotation;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.transform.SetPositionAndRotation(position, rotation);
    }

    private static Vector3 GetSegmentNormal(Vector3 a, Vector3 b)
    {
        var direction = (b - a).normalized;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector3.forward;
        }

        return Vector3.Cross(Vector3.up, direction).normalized;
    }
}
