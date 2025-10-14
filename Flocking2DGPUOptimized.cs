using UnityEngine;

public class Flocking2DGPUOptimized : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader flockingShader;

    [Header("Rendering")]
    [SerializeField] private Mesh unitMesh;
    [SerializeField] private Material unitMaterial;
    [SerializeField] private float unitScale = 0.5f;

    [Header("Unit Settings")]
    [SerializeField] private int unitCount = 5000;
    [SerializeField] private Vector2 boundsMin = new Vector2(-20, -20);
    [SerializeField] private Vector2 boundsMax = new Vector2(20, 20);

    [Header("Flocking Settings")]
    [SerializeField] private float separationRadius = 1f;
    [SerializeField] private float alignmentRadius = 3f;
    [SerializeField] private float cohesionRadius = 3f;
    [SerializeField] private float separationWeight = 1.5f;
    [SerializeField] private float alignmentWeight = 1.0f;
    [SerializeField] private float cohesionWeight = 1.0f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float maxForce = 3f;

    // GPU 버퍼 (CPU 복사 없음)
    private ComputeBuffer unitBuffer;
    private ComputeBuffer argsBuffer;
    private int kernelIndex;

    // DrawIndirect용 args
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    private void Start()
    {
        InitializeBuffers();
        InitializeComputeShader();
        InitializeMaterial();
    }

    private void InitializeBuffers()
    {
        // 유닛 데이터 초기화
        UnitData2D[] unitDataArray = new UnitData2D[unitCount];

        for (int i = 0; i < unitCount; i++)
        {
            unitDataArray[i] = new UnitData2D
            {
                position = new Vector2(
                    Random.Range(boundsMin.x, boundsMax.x),
                    Random.Range(boundsMin.y, boundsMax.y)
                ),
                velocity = Random.insideUnitCircle.normalized * maxSpeed * 0.5f
            };
        }

        // ComputeBuffer 생성
        int stride = sizeof(float) * 2 * 2;
        unitBuffer = new ComputeBuffer(unitCount, stride);
        unitBuffer.SetData(unitDataArray);

        // DrawMeshInstancedIndirect용 args 버퍼
        args[0] = unitMesh.GetIndexCount(0);
        args[1] = (uint)unitCount;
        args[2] = unitMesh.GetIndexStart(0);
        args[3] = unitMesh.GetBaseVertex(0);
        args[4] = 0;

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    private void InitializeComputeShader()
    {
        kernelIndex = flockingShader.FindKernel("CSFlocking");

        flockingShader.SetBuffer(kernelIndex, "units", unitBuffer);
        flockingShader.SetInt("unitCount", unitCount);
        flockingShader.SetFloat("separationRadius", separationRadius);
        flockingShader.SetFloat("alignmentRadius", alignmentRadius);
        flockingShader.SetFloat("cohesionRadius", cohesionRadius);
        flockingShader.SetFloat("separationWeight", separationWeight);
        flockingShader.SetFloat("alignmentWeight", alignmentWeight);
        flockingShader.SetFloat("cohesionWeight", cohesionWeight);
        flockingShader.SetFloat("maxSpeed", maxSpeed);
        flockingShader.SetFloat("maxForce", maxForce);
        flockingShader.SetVector("boundsMin", boundsMin);
        flockingShader.SetVector("boundsMax", boundsMax);
    }

    private void InitializeMaterial()
    {
        // Shader에 버퍼 전달
        unitMaterial.SetBuffer("_UnitsBuffer", unitBuffer);
        unitMaterial.SetFloat("_UnitScale", unitScale);
    }

    private void Update()
    {
        // Compute Shader 실행 (GPU만)
        flockingShader.SetFloat("deltaTime", Time.deltaTime);

        int threadGroups = Mathf.CeilToInt(unitCount / 64f);
        flockingShader.Dispatch(kernelIndex, threadGroups, 1, 1);

        // GPU에서 GPU로 바로 렌더링 (CPU 관여 없음)
        Graphics.DrawMeshInstancedIndirect(
            unitMesh,
            0,
            unitMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f), // 컬링 범위
            argsBuffer
        );
    }

    private void OnDestroy()
    {
        unitBuffer?.Release();
        argsBuffer?.Release();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Vector3 center = new Vector3(
            (boundsMin.x + boundsMax.x) * 0.5f,
            (boundsMin.y + boundsMax.y) * 0.5f,
            0
        );
        Vector3 size = new Vector3(
            boundsMax.x - boundsMin.x,
            boundsMax.y - boundsMin.y,
            0.1f
        );
        Gizmos.DrawWireCube(center, size);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 300, 80), "Flocking GPU 2D (Zero Copy)");
        GUI.Label(new Rect(20, 35, 280, 20), $"Units: {unitCount}");
        GUI.Label(new Rect(20, 55, 280, 20), $"FPS: {1f / Time.deltaTime:F0}");
        GUI.Label(new Rect(20, 75, 280, 20), "Mode: Full GPU (No CPU Copy)");
    }
}