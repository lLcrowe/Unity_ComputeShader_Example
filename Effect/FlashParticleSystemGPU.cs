using UnityEngine;

public class FlashParticleSystemGPU : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private int flashCount = 1000;
    [SerializeField] private float flashLifetime = 0.15f;
    [SerializeField] private float explosionForce = 10f;
    [SerializeField] private float explosionRadius = 2f;

    [Header("References")]
    [SerializeField] private ComputeShader flashCompute;
    [SerializeField] private Material flashMaterial;
    [SerializeField] private Mesh particleMesh; // Quad

    [Header("Gizmos")]
    [SerializeField] private bool showExplosionRadius = true;

    private ComputeBuffer flashBuffer;
    private ComputeBuffer argsBuffer;
    private int updateFlashKernel;
    private FlashParticle[] flashParticles;

    private const int FLASH_STRIDE = 32; // 8 floats × 4 bytes
    private static readonly int FlashBufferID = Shader.PropertyToID("flashBuffer");
    private static readonly int DeltaTimeID = Shader.PropertyToID("deltaTime");
    private static readonly int ParticleCountID = Shader.PropertyToID("particleCount");

    // Flash 파티클 구조체 (CPU 측)
    private struct FlashParticle
    {
        public Vector3 Position;//12
        public Vector3 Velocity;//12
        public float Lifetime;//4
        public float Brightness;//4
    }


    private void OnEnable()
    {

        ValidateReferences();
        InitializeKernels();
        InitializeBuffers();
        BindShaderProperties();
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void Update()
    {
        //if (Input.GetKey(KeyCode.Space))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f)
            );
            Explode(mouseWorldPos);
        }

        UpdateFlashParticles();
    }

    private void LateUpdate()
    {
        RenderFlashParticles();
    }

    private void ValidateReferences()
    {
        if (flashCompute == null)
        {
            Debug.LogError("FlashCompute is not assigned!", this);
        }

        if (flashMaterial == null)
        {
            Debug.LogError("FlashMaterial is not assigned!", this);
        }

        if (particleMesh == null)
        {
            Debug.LogError("ParticleMesh is not assigned! Assign a Quad mesh.", this);
        }
    }

    private void InitializeKernels()
    {
        updateFlashKernel = flashCompute.FindKernel("UpdateFlash");
    }

    private void InitializeBuffers()
    {
        // Flash 파티클 버퍼
        flashBuffer = new ComputeBuffer(flashCount, FLASH_STRIDE);
        flashParticles = new FlashParticle[flashCount];

        // 모든 파티클 비활성화 (Lifetime = 0)
        for (int i = 0; i < flashCount; i++)
        {
            flashParticles[i].Lifetime = 0f;
        }
        flashBuffer.SetData(flashParticles);

        // Indirect Rendering Args 버퍼
        uint[] args = new uint[5];
        args[0] = particleMesh.GetIndexCount(0); // index count per instance
        args[1] = (uint)flashCount;              // instance count
        args[2] = particleMesh.GetIndexStart(0); // start index location
        args[3] = particleMesh.GetBaseVertex(0); // base vertex location
        args[4] = 0;                             // start instance location

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    private void BindShaderProperties()
    {
        flashCompute.SetBuffer(updateFlashKernel, FlashBufferID, flashBuffer);
        flashCompute.SetInt(ParticleCountID, flashCount);

        flashMaterial.SetBuffer(FlashBufferID, flashBuffer);
    }

    private void ReleaseBuffers()
    {
        if (flashBuffer != null)
        {
            flashBuffer.Release();
            flashBuffer = null;
        }

        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }
    }

    public void Explode(Vector3 position)
    {
        for (int i = 0; i < flashCount; i++)
        {
            // 구 표면에 균등 분포
            Vector3 randomDir = Random.onUnitSphere;
            float randomSpeed = Random.Range(0.5f, 1f) * explosionForce;

            flashParticles[i].Position = position + randomDir * explosionRadius * 0.1f;
            flashParticles[i].Velocity = randomDir * randomSpeed;
            flashParticles[i].Lifetime = flashLifetime;
            flashParticles[i].Brightness = Random.Range(0.8f, 1.2f);
        }

        flashBuffer.SetData(flashParticles);
    }

    private void UpdateFlashParticles()
    {
        flashCompute.SetFloat(DeltaTimeID, Time.deltaTime);

        int threadGroups = Mathf.CeilToInt(flashCount / 256f);
        flashCompute.Dispatch(updateFlashKernel, threadGroups, 1, 1);
    }

    private void RenderFlashParticles()
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            flashMaterial,
            bounds,
            argsBuffer
        );
    }

    private void OnDrawGizmos()
    {
        if (!showExplosionRadius) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}