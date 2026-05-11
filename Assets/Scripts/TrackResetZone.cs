using UnityEngine;

public sealed class TrackResetZone : MonoBehaviour
{
    [SerializeField] private AStarGrid grid;
    [SerializeField] private float resetHeight = 1.1f;

    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void Configure(AStarGrid newGrid)
    {
        grid = newGrid;
    }

    private void Update()
    {
        if (grid == null)
        {
            return;
        }

        var closest = grid.FindClosestNode(transform.position);
        if (closest == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetToTrack(closest);
        }
    }

    private void ResetToTrack(Transform node)
    {
        var nextIndex = (grid.IndexOf(node) + 1) % grid.Nodes.Count;
        var next = grid.Nodes[nextIndex];
        var forward = (next.position - node.position).normalized;

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        transform.position = new Vector3(node.position.x, 0f, node.position.z) + Vector3.up * resetHeight;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
