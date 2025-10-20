using UnityEngine;

public class Flocking2DGPU : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField] private ComputeShader flockingShader;

    [Header("Unit Settings")]
    [SerializeField] private int unitCount = 500;
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

    [Header("Visualization")]
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private bool useInstancing = true;
    [SerializeField] private Mesh unitMesh;
    [SerializeField] private Material unitMaterial;

    // GPU 버퍼
    private ComputeBuffer unitBuffer;
    private UnitData2D[] unitDataArray;
    private int kernelIndex;

    // 렌더링
    private GameObject[] unitObjects;
    private Matrix4x4[] matrices;
    private MaterialPropertyBlock propertyBlock;

    private void Start()
    {
        InitializeUnits();
        InitializeComputeShader();

        if (useInstancing)
        {
            InitializeInstancing();
        }
        else
        {
            InitializeGameObjects();
        }
    }

    private void InitializeUnits()
    {
        unitDataArray = new UnitData2D[unitCount];

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
    }

    private void InitializeComputeShader()
    {
        kernelIndex = flockingShader.FindKernel("CSFlocking");

        // 버퍼 생성 (stride = 16 bytes: Vector2 * 2)
        int stride = sizeof(float) * 2 * 2;
        unitBuffer = new ComputeBuffer(unitCount, stride);
        unitBuffer.SetData(unitDataArray);

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

    private void InitializeGameObjects()
    {
        unitObjects = new GameObject[unitCount];

        for (int i = 0; i < unitCount; i++)
        {
            unitObjects[i] = Instantiate(unitPrefab, transform);
            unitObjects[i].name = $"Unit_{i}";
        }
    }

    private void InitializeInstancing()
    {
        matrices = new Matrix4x4[unitCount];
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        UpdateFlocking();

        if (useInstancing)
        {
            RenderInstanced();
        }
        else
        {
            RenderGameObjects();
        }
    }

    private void UpdateFlocking()
    {
        flockingShader.SetFloat("deltaTime", Time.deltaTime);

        int threadGroups = Mathf.CeilToInt(unitCount / 64f);
        flockingShader.Dispatch(kernelIndex, threadGroups, 1, 1);

        unitBuffer.GetData(unitDataArray);
    }

    private void RenderGameObjects()
    {
        for (int i = 0; i < unitCount; i++)
        {
            Vector3 pos = new Vector3(
                unitDataArray[i].position.x,
                unitDataArray[i].position.y,
                0
            );

            float angle = Mathf.Atan2(
                unitDataArray[i].velocity.y,
                unitDataArray[i].velocity.x
            ) * Mathf.Rad2Deg;

            unitObjects[i].transform.position = pos;
            unitObjects[i].transform.rotation = Quaternion.Euler(0, 0, angle - 90);
        }
    }

    private void RenderInstanced()
    {
        for (int i = 0; i < unitCount; i++)
        {
            Vector3 pos = new Vector3(
                unitDataArray[i].position.x,
                unitDataArray[i].position.y,
                0
            );

            float angle = Mathf.Atan2(
                unitDataArray[i].velocity.y,
                unitDataArray[i].velocity.x
            ) * Mathf.Rad2Deg;

            Quaternion rot = Quaternion.Euler(0, 0, angle - 90);

            matrices[i] = Matrix4x4.TRS(pos, rot, Vector3.one * 0.5f);
        }

        //인스턴싱
        Graphics.DrawMeshInstanced(
            unitMesh,
            0,
            unitMaterial,
            matrices,
            unitCount,
            propertyBlock
        );
    }

    private void OnDestroy()
    {
        unitBuffer?.Release();
    }

    private void OnDrawGizmos()
    {
        // 경계 그리기
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

        // 첫 유닛 반경 시각화
        if (Application.isPlaying && unitDataArray != null && unitDataArray.Length > 0)
        {
            Vector3 pos = new Vector3(unitDataArray[0].position.x, unitDataArray[0].position.y, 0);

            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(pos, separationRadius);

            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(pos, alignmentRadius);

            Gizmos.color = new Color(0, 0, 1, 0.3f);
            Gizmos.DrawWireSphere(pos, cohesionRadius);
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 250, 100), "Flocking GPU 2D");
        GUI.Label(new Rect(20, 35, 200, 20), $"Units: {unitCount}");
        GUI.Label(new Rect(20, 55, 200, 20), $"FPS: {1f / Time.deltaTime:F0}");
        GUI.Label(new Rect(20, 75, 200, 20), $"Mode: {(useInstancing ? "Instancing" : "GameObjects")}");
    }
}