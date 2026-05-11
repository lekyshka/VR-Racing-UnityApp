using System.Collections.Generic;
using UnityEngine;

public sealed class ProceduralTrackGenerator : MonoBehaviour
{
    private const int NodeCount = 14;
    private const float RoadY = 0.02f;
    private const float CurbY = 0.18f;
    private const float WallY = 0.75f;

    [SerializeField] private AStarGrid grid;
    [SerializeField] private RaceSettings settings;
    [SerializeField] private bool randomizeEveryRace = true;
    [SerializeField] private float trackRadius = 92f;
    [SerializeField] private float trackWidth = 24f;
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
    private Vector3[] centerline;

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

        var seed = randomizeEveryRace ? System.Environment.TickCount : 20260512;
        centerline = BuildCenterline(seed);

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

        points[0] = new Vector3(-44f, 0f, -78f);
        points[1] = new Vector3(18f, 0f, -78f);

        for (var i = 2; i < NodeCount; i++)
        {
            var angle = Mathf.PI * 2f * i / NodeCount;
            var radiusNoise = Mathf.Lerp(0.78f, 1.18f, (float)random.NextDouble());
            var x = Mathf.Cos(angle) * trackRadius * radiusNoise;
            var z = Mathf.Sin(angle) * trackRadius * Mathf.Lerp(0.72f, 1.12f, (float)random.NextDouble());
            points[i] = new Vector3(x, 0f, z);
        }

        return SmoothPoints(points);
    }

    private static Vector3[] SmoothPoints(Vector3[] points)
    {
        var result = new Vector3[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var previous = points[(i - 1 + points.Length) % points.Length];
            var current = points[i];
            var next = points[(i + 1) % points.Length];
            result[i] = (previous + current * 2f + next) * 0.25f;
        }

        result[0] = new Vector3(-44f, 0f, -78f);
        result[1] = new Vector3(18f, 0f, -78f);
        return result;
    }

    private void BuildRoadSurface()
    {
        var mesh = BuildRibbonMesh(centerline, trackWidth, RoadY, false, 0.02f);
        var road = CreateMeshObject("Generated Continuous Road Surface", mesh, roadMaterial, true);
        road.layer = 0;
    }

    private void BuildCurbSurface()
    {
        var curbMesh = new Mesh { name = "Generated Continuous Curb Mesh" };
        var vertices = new List<Vector3>();
        var redTriangles = new List<int>();
        var whiteTriangles = new List<int>();

        AppendCurbSide(vertices, redTriangles, whiteTriangles, true);
        AppendCurbSide(vertices, redTriangles, whiteTriangles, false);

        curbMesh.SetVertices(vertices);
        curbMesh.subMeshCount = 2;
        curbMesh.SetTriangles(redTriangles, 0);
        curbMesh.SetTriangles(whiteTriangles, 1);
        curbMesh.RecalculateNormals();
        curbMesh.RecalculateBounds();

        var curb = CreateMeshObject("Generated Continuous Red White Curbs", curbMesh, null, true);
        curb.GetComponent<MeshRenderer>().sharedMaterials = new[] { curbRedMaterial, curbWhiteMaterial };
    }

    private void AppendCurbSide(List<Vector3> vertices, List<int> redTriangles, List<int> whiteTriangles, bool leftSide)
    {
        for (var i = 0; i < centerline.Length; i++)
        {
            var nextIndex = (i + 1) % centerline.Length;
            var current = centerline[i];
            var next = centerline[nextIndex];
            var normal = GetSegmentNormal(current, next) * (leftSide ? 1f : -1f);
            var innerA = current + normal * (trackWidth * 0.5f);
            var outerA = current + normal * (trackWidth * 0.5f + curbWidth);
            var innerB = next + normal * (trackWidth * 0.5f);
            var outerB = next + normal * (trackWidth * 0.5f + curbWidth);

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
        CreateMeshObject("Generated Left Collision Wall", BuildWallMesh(true), wallMaterial, true);
        CreateMeshObject("Generated Right Collision Wall", BuildWallMesh(false), wallMaterial, true);
    }

    private Mesh BuildWallMesh(bool leftSide)
    {
        var mesh = new Mesh { name = leftSide ? "Generated Left Wall Mesh" : "Generated Right Wall Mesh" };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var side = leftSide ? 1f : -1f;
        var offset = trackWidth * 0.5f + curbWidth + wallWidth * 0.5f;

        for (var i = 0; i < centerline.Length; i++)
        {
            var nextIndex = (i + 1) % centerline.Length;
            var current = centerline[i];
            var next = centerline[nextIndex];
            var normal = GetSegmentNormal(current, next) * side;
            var bottomA = current + normal * offset;
            var bottomB = next + normal * offset;

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

    private Mesh BuildRibbonMesh(IReadOnlyList<Vector3> points, float width, float y, bool sideOnly, float offset)
    {
        var mesh = new Mesh { name = "Generated Ribbon Mesh" };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (var i = 0; i < points.Count; i++)
        {
            var nextIndex = (i + 1) % points.Count;
            var current = points[i];
            var next = points[nextIndex];
            var normal = GetSegmentNormal(current, next);

            Vector3 leftA;
            Vector3 rightA;
            Vector3 leftB;
            Vector3 rightB;

            if (sideOnly)
            {
                leftA = current + normal * (offset + width * 0.5f);
                rightA = current + normal * (offset - width * 0.5f);
                leftB = next + normal * (offset + width * 0.5f);
                rightB = next + normal * (offset - width * 0.5f);
            }
            else if (offset > 0.1f)
            {
                leftA = current - normal * (offset - width * 0.5f);
                rightA = current - normal * (offset + width * 0.5f);
                leftB = next - normal * (offset - width * 0.5f);
                rightB = next - normal * (offset + width * 0.5f);
            }
            else
            {
                leftA = current + normal * (width * 0.5f);
                rightA = current - normal * (width * 0.5f);
                leftB = next + normal * (width * 0.5f);
                rightB = next - normal * (width * 0.5f);
            }

            var start = vertices.Count;
            vertices.Add(new Vector3(leftA.x, y, leftA.z));
            vertices.Add(new Vector3(rightA.x, y, rightA.z));
            vertices.Add(new Vector3(leftB.x, y, leftB.z));
            vertices.Add(new Vector3(rightB.x, y, rightB.z));

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
