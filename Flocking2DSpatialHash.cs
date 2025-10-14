using UnityEngine;

public class FlockingSpatialHash : MonoBehaviour
{
    [Header("Assets")]
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Material material;
    [SerializeField] private Mesh unitMesh; // 외부에서 할당

    [Header("Settings")]
    [SerializeField] private int unitCount = 10000;
    [SerializeField] private float cellSize = 5f;
    [SerializeField] private float unitScale = 1f;

    [Header("Bounds")]
    [SerializeField] private float boundsSize = 50f;

    [Header("Flocking")]
    [SerializeField] private float separationRadius = 1f;
    [SerializeField] private float alignmentRadius = 3f;
    [SerializeField] private float cohesionRadius = 3f;
    [SerializeField] private float separationWeight = 1.5f;
    [SerializeField] private float alignmentWeight = 1f;
    [SerializeField] private float cohesionWeight = 1f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float maxForce = 3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugSphere = true;
    [SerializeField] private bool logDebugInfo = true;

    private ComputeBuffer unitBuffer;
    private ComputeBuffer gridBuffer;
    private ComputeBuffer argsBuffer;

    private int clearKernel;
    private int buildKernel;
    private int flockingKernel;

    private int gridWidth;
    private int gridHeight;
    private int gridTotal;

    private float debugTimer = 0f;

    void Start()
    {
        if (unitMesh == null)
        {
            Debug.LogError("Unit Mesh is NULL! Assign a mesh in Inspector.");
            return;
        }

        if (computeShader == null)
        {
            Debug.LogError("Compute Shader is NULL!");
            return;
        }

        if (material == null)
        {
            Debug.LogError("Material is NULL!");
            return;
        }

        Debug.Log("=== Starting Flocking ===");

        SetupGrid();
        SetupBuffers();
        SetupCompute();
        SetupMaterial();

        Debug.Log($"Setup complete. Units: {unitCount}, Grid: {gridWidth}x{gridHeight}");
    }

    void SetupGrid()
    {
        float bounds = boundsSize * 2f;
        gridWidth = Mathf.CeilToInt(bounds / cellSize);
        gridHeight = Mathf.CeilToInt(bounds / cellSize);
        gridTotal = gridWidth * gridHeight;

        Debug.Log($"Grid: {gridWidth}x{gridHeight} = {gridTotal} cells");
    }

    void SetupBuffers()
    {
        Vector4[] data = new Vector4[unitCount];

        // 초기값 확실하게 설정
        for (int i = 0; i < unitCount; i++)
        {
            Vector2 vel = Random.insideUnitCircle;
            if (vel.magnitude < 0.1f) // 너무 작으면 강제 설정
            {
                vel = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            }
            vel = vel.normalized * maxSpeed * 0.5f; // 초기 속도의 50%

            data[i] = new Vector4(
                Random.Range(-boundsSize * 0.8f, boundsSize * 0.8f), // x
                Random.Range(-boundsSize * 0.8f, boundsSize * 0.8f), // y
                vel.x, // vx
                vel.y  // vy
            );
        }

        unitBuffer = new ComputeBuffer(unitCount, 16);
        unitBuffer.SetData(data);

        // 디버그: 첫 5개 유닛 확인
        if (logDebugInfo)
        {
            for (int i = 0; i < Mathf.Min(5, unitCount); i++)
            {
                Debug.Log($"Unit {i}: pos=({data[i].x:F1},{data[i].y:F1}) vel=({data[i].z:F2},{data[i].w:F2})");
            }
        }

        gridBuffer = new ComputeBuffer(gridTotal, 132);

        uint[] args = new uint[5];
        args[0] = unitMesh.GetIndexCount(0);
        args[1] = (uint)unitCount;
        args[2] = unitMesh.GetIndexStart(0);
        args[3] = unitMesh.GetBaseVertex(0);
        args[4] = 0;

        argsBuffer = new ComputeBuffer(1, 20, ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        Debug.Log($"Unit buffer: {unitCount} units, Args: {args[0]} indices, {args[1]} instances");
    }

    void SetupCompute()
    {
        clearKernel = computeShader.FindKernel("CSClearGrid");
        buildKernel = computeShader.FindKernel("CSBuildGrid");
        flockingKernel = computeShader.FindKernel("CSFlocking");

        computeShader.SetInt("unitCount", unitCount);
        computeShader.SetVector("boundsMin", new Vector2(-boundsSize, -boundsSize));
        computeShader.SetVector("boundsMax", new Vector2(boundsSize, boundsSize));
        computeShader.SetFloat("cellSize", cellSize);
        computeShader.SetInt("gridWidth", gridWidth);
        computeShader.SetInt("gridHeight", gridHeight);
        computeShader.SetFloat("separationRadius", separationRadius);
        computeShader.SetFloat("alignmentRadius", alignmentRadius);
        computeShader.SetFloat("cohesionRadius", cohesionRadius);
        computeShader.SetFloat("separationWeight", separationWeight);
        computeShader.SetFloat("alignmentWeight", alignmentWeight);
        computeShader.SetFloat("cohesionWeight", cohesionWeight);
        computeShader.SetFloat("maxSpeed", maxSpeed);
        computeShader.SetFloat("maxForce", maxForce);

        computeShader.SetBuffer(clearKernel, "grid", gridBuffer);
        computeShader.SetBuffer(buildKernel, "units", unitBuffer);
        computeShader.SetBuffer(buildKernel, "grid", gridBuffer);
        computeShader.SetBuffer(flockingKernel, "units", unitBuffer);
        computeShader.SetBuffer(flockingKernel, "grid", gridBuffer);

        Debug.Log("Compute shader setup complete");
    }

    void SetupMaterial()
    {
        material.SetBuffer("_UnitsBuffer", unitBuffer);
        material.SetFloat("_UnitScale", unitScale);

        Debug.Log($"Material setup: {material.name}, Shader: {material.shader.name}");
    }

    void Update()
    {
        if (unitBuffer == null || computeShader == null) return;

        // 디버그: 2초마다 첫 유닛 위치 출력
        debugTimer += Time.deltaTime;
        if (logDebugInfo && debugTimer > 2f)
        {
            debugTimer = 0f;
            Vector4[] debugData = new Vector4[1];
            unitBuffer.GetData(debugData, 0, 0, 1);
            Debug.Log($"Frame {Time.frameCount}: Unit 0 pos=({debugData[0].x:F1},{debugData[0].y:F1}) vel=({debugData[0].z:F2},{debugData[0].w:F2})");
        }

        // Compute 실행
        int clearGroups = Mathf.CeilToInt(gridTotal / 256f);
        computeShader.Dispatch(clearKernel, clearGroups, 1, 1);

        int unitGroups = Mathf.CeilToInt(unitCount / 256f);
        computeShader.Dispatch(buildKernel, unitGroups, 1, 1);

        computeShader.SetFloat("deltaTime", Time.deltaTime);
        computeShader.Dispatch(flockingKernel, unitGroups, 1, 1);

        // 렌더링
        Bounds renderBounds = new Bounds(
            Vector3.zero,
            new Vector3(boundsSize * 3f, boundsSize * 3f, 100f)
        );

        Graphics.DrawMeshInstancedIndirect(
            unitMesh,
            0,
            material,
            renderBounds,
            argsBuffer,
            0,
            null,
            UnityEngine.Rendering.ShadowCastingMode.Off,
            false
        );
    }

    void OnDestroy()
    {
        unitBuffer?.Release();
        gridBuffer?.Release();
        argsBuffer?.Release();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boundsSize * 2f, boundsSize * 2f, 1f));

        if (showDebugSphere)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Vector3.zero, 5f);
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 140));

        GUILayout.Box("Spatial Hash Flocking", GUILayout.Width(290));
        GUILayout.Label($"Units: {unitCount:N0}");
        GUILayout.Label($"Grid: {gridWidth}x{gridHeight} = {gridTotal}");
        GUILayout.Label($"FPS: {1f / Time.deltaTime:F0}");
        GUILayout.Label($"Frame: {Time.deltaTime * 1000f:F1}ms");
        GUILayout.Label($"Time: {Time.time:F1}s");

        GUILayout.EndArea();
    }
}