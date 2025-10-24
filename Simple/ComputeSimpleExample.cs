using UnityEngine;
using UnityEngine.UI;

public class ComputeSimpleExample : MonoBehaviour
{
    public Button example1Btn;
    public Button example2Btn;


    [Header("ComputeShader")]
    [SerializeField] private ComputeShader computeShader;

    [Header("Example 1: 배열 2배 만들기")]
    [SerializeField] private int arraySize = 10;

    [Header("Example 2: 픽셀 카운터")]
    [SerializeField] private Texture2D targetTexture;
    [SerializeField, Range(0f, 1f)] private float blackThreshold = 0.1f;

    private void Awake()
    {
        example1Btn.onClick.AddListener(Example1_MultiplyArray);
        example2Btn.onClick.AddListener(Example2_CountBlackPixels);
    }


    // ==================== 예제 1: 배열 2배 만들기 ====================
    private void Example1_MultiplyArray()
    {
        Debug.Log("【예제 1】 배열 데이터 2배로 만들기");
        Debug.Log("─────────────────────────────────────");

        // === 1. CPU 데이터 준비 ===
        float[] inputData = new float[arraySize];
        for (int i = 0; i < arraySize; i++)
        {
            inputData[i] = i;
        }
        Debug.Log($"[CPU 입력] {string.Join(", ", inputData)}");


        // === 2. GPU 버퍼 생성 ===
        //크기//데이터타입크기
        ComputeBuffer buffer = new ComputeBuffer(arraySize, sizeof(float));

        // === 3. CPU → GPU 전송 ===
        buffer.SetData(inputData);
        Debug.Log("→ CPU에서 GPU로 데이터 전송");


        // == 4. Kernel 설정 ==
        int kernelID = computeShader.FindKernel("MultiplyByTwo");
        computeShader.SetBuffer(kernelID, "numbers", buffer);


        // == 5. Dispatch 계산 및 실행 ==
        computeShader.GetKernelThreadGroupSizes(kernelID, out uint threadX, out _, out _);
        //int numThreads = 64;
        int numThreads = (int)threadX;
        int dispatchGroups = Mathf.CeilToInt(arraySize / (float)numThreads);
        Debug.Log($"→ Dispatch: {dispatchGroups} 그룹 (총 {dispatchGroups * numThreads}개 스레드)");

        computeShader.Dispatch(kernelID, dispatchGroups, 1, 1);
        Debug.Log("→ GPU에서 계산 실행 중...");


        // == 6. GPU → CPU 결과 받기 ==
        float[] outputData = new float[arraySize];
        buffer.GetData(outputData);
        Debug.Log($"[GPU 출력] {string.Join(", ", outputData)}");


        // == 7. 검증 ==
        bool success = true;
        for (int i = 0; i < arraySize; i++)
        {
            if (outputData[i] != inputData[i] * 2)
            {
                success = false;
                break;
            }
        }
        Debug.Log($"✓ 검증 결과: {(success ? "성공" : "실패")}");


        // === 8. 메모리 해제 ===
        buffer.Release();
        Debug.Log("→ GPU 메모리 해제 완료");
    }


    // ==================== 예제 2: 검은색 픽셀 카운트 ====================
    private void Example2_CountBlackPixels()
    {
        Debug.Log("【예제 2】 텍스처에서 검은색 픽셀 카운트");
        Debug.Log("─────────────────────────────────────");

        // === 0. 텍스처 체크 ===
        if (targetTexture == null)
        {
            Debug.LogWarning("⚠ Target Texture가 설정되지 않았습니다!");
            return;
        }

        int width = targetTexture.width;
        int height = targetTexture.height;
        int totalPixels = width * height;

        Debug.Log($"텍스처 크기: {width} × {height} = {totalPixels:N0} 픽셀");
        Debug.Log($"검은색 기준: 밝기 {blackThreshold} 이하");


        // === 1. 카운터 버퍼 생성 ===
        ComputeBuffer counterBuffer = new ComputeBuffer(1, sizeof(int));
        int[] initialCount = new int[1] { 0 };
        counterBuffer.SetData(initialCount);


        // === 2. Kernel 설정 ===
        int kernelIndex = computeShader.FindKernel("CountBlackPixels");
        computeShader.SetTexture(kernelIndex, "InputTexture", targetTexture);
        computeShader.SetBuffer(kernelIndex, "Counter", counterBuffer);
        computeShader.SetFloat("BlackThreshold", blackThreshold);
        computeShader.SetInt("Width", width);
        computeShader.SetInt("Height", height);


        // === 3. Dispatch 계산 ===
        int threadGroupsX = Mathf.CeilToInt(width / 8f);
        int threadGroupsY = Mathf.CeilToInt(height / 8f);
        int totalThreads = threadGroupsX * threadGroupsY * 64;

        Debug.Log($"→ Dispatch: ({threadGroupsX}, {threadGroupsY}, 1)");
        Debug.Log($"→ 총 실행 스레드: {totalThreads:N0}개");


        // === 4. GPU 실행 ===
        computeShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
        Debug.Log("→ GPU에서 실행...");


        // === 5. 결과 받기 ===
        int[] result = new int[1];
        counterBuffer.GetData(result);

        int blackPixelCount = result[0];
        float percentage = (blackPixelCount / (float)totalPixels) * 100f;

        Debug.Log($"[결과] 검은색 픽셀: {blackPixelCount:N0}개 ({percentage:F2}%)");
        Debug.Log($"       나머지 픽셀: {totalPixels - blackPixelCount:N0}개");


        // === 6. 메모리 해제 ===
        counterBuffer.Release();
        Debug.Log("→ GPU 메모리 해제 완료");
    }


    // ==================== Inspector 버튼 ====================
    [ContextMenu("예제 1: 배열 2배")]
    private void RunExample1()
    {
        Example1_MultiplyArray();
    }

    [ContextMenu("예제 2: 픽셀 카운트")]
    private void RunExample2()
    {
        Example2_CountBlackPixels();
    }
}
