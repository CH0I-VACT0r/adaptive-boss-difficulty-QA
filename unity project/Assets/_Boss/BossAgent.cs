using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.IO;

public class BossAgent : Agent
{
    [Header("Target Settings : 20% ~ 80%")]
    [Tooltip("전체 유저 대상 통합 목표 클리어율 (예: 0.5 = 50%)")]
    [Range(0f, 1f)] public float targetClearRate = 0.5f;

    [Header("Batch Evaluation")]
    public int episodesPerBatch = 100;
    public int batchesPerBossEpisode = 10;
    private int currentBatchCount = 0;
    private int currentEpisodeCount = 0;
    private int playerClearCount = 0;
    private float totalAccumulatedDamage = 0f;
    private float lastActualClearRate = -1f;

    private int[] personaSpawnCounts = new int[3];
    private int[] personaClearCounts = new int[3];

    [Header("Production Test (Hardcoded Balance)")]
    [Tooltip("체크 시 인공지능 추론을 멈추고 아래 고정값으로 테스트합니다.")]
    public bool useHardcodedBalance = false;
    public float bestCooldown = 1.228f;
    public float[] bestWeights = new float[4] { 0.49f, 0f, 0.34f, 0.17f };

    [Header("References")]
    public BossPatternManager patternManager;
    public PlayerController player;

    [Header("Current Output (Debug)")]
    public float currentCooldown;
    public float[] currentPatternWeights = new float[4];

    public override void OnEpisodeBegin()
    {
        currentCooldown = 1.5f;
        for (int i = 0; i < 4; i++)
        {
            currentPatternWeights[i] = 0.5f;
        }

        currentBatchCount = 0;
        lastActualClearRate = -1f;

        // 첫 번째 배치를 시작하기 전, 첫 유저 셋팅
        SetupRandomPlayer();
        StartNewBatch();
    }

    // --- [EDPCG 핵심 로직] 매 판마다 무작위 유저가 입장함 ---
    private void SetupRandomPlayer()
    {
        player.currentPersona = (PlayerController.PersonaType)Random.Range(0, 3);
        player.currentDPS = Random.Range(40f, 80f);
    }

    private void StartNewBatch()
    {
        currentEpisodeCount = 0;
        playerClearCount = 0;
        totalAccumulatedDamage = 0f;

        for (int i = 0; i < 3; i++)
        {
            personaSpawnCounts[i] = 0;
            personaClearCounts[i] = 0;
        }

        if (useHardcodedBalance)
        {
            ApplyHardcodedBalance();
        }
        else
        {
            RequestDecision(); // 기존 AI 추론 모드
        }
    }

    private void ApplyHardcodedBalance()
    {
        currentCooldown = bestCooldown;
        for (int i = 0; i < 4; i++)
        {
            currentPatternWeights[i] = bestWeights[i];
        }

        if (patternManager != null)
        {
            patternManager.patternCooldown = currentCooldown;
            patternManager.UpdatePatternWeights(currentPatternWeights);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(targetClearRate);       // 기획자 전체 목표
        sensor.AddObservation(lastActualClearRate);   // 이전 20명 대상 실제 클리어율

        sensor.AddObservation(currentCooldown / 3.0f);
        for (int i = 0; i < 4; i++)
        {
            sensor.AddObservation(currentPatternWeights[i]);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuousActions = actions.ContinuousActions;

        float normalizedCooldown = (continuousActions[0] + 1f) / 2f;
        currentCooldown = Mathf.Lerp(0.1f, 3.0f, normalizedCooldown);

        for (int i = 0; i < 4; i++)
        {
            currentPatternWeights[i] = Mathf.Max(0.01f, (continuousActions[i + 1] + 1f) / 2f);
        }

        if (patternManager != null)
        {
            patternManager.patternCooldown = currentCooldown;
            patternManager.UpdatePatternWeights(currentPatternWeights);
        }
    }

    public void ReportPlayerResult(bool isClear, float playerDamage)
    {
        currentEpisodeCount++;
        totalAccumulatedDamage += playerDamage;

        int personaIndex = (int)player.currentPersona;
        personaSpawnCounts[personaIndex]++;

        if (isClear)
        {
            playerClearCount++;
            personaClearCounts[personaIndex]++; 
        }

        if (currentEpisodeCount >= episodesPerBatch)
        {
            EvaluateBatch();
        }
        else
        {
            SetupRandomPlayer();
            player.EndEpisode();
        }
    }

    private void EvaluateBatch()
    {
        float actualClearRate = (float)playerClearCount / episodesPerBatch;
        float error = Mathf.Abs(targetClearRate - actualClearRate);

        // [대조군 실험 1] 선형 보상 함수 + 허용 오차(Tolerance)
        // =============================================================
        //float tolerance = 0.05f;
        //float reward = 0f;

        //if (error <= tolerance)
        //{
        //    reward = 1.0f; // 오차 5% 이내 진입 시 만점 부여 (가우시안과 동일 조건)
        //}
        //else
        //{
        //    // 허용 오차를 초과한 시점부터 1.0점 ~ -1.0점 사이로 선형 감소
        //    float adjustedError = error - tolerance;
        //    float maxPossibleError = 1.0f - tolerance; // 0.95

        //    // 선형 보간: 오차가 커질수록 최대 2.0점의 폭을 선형적으로 깎아내림
        //    reward = 1.0f - ((adjustedError / maxPossibleError) * 2.0f);
        //}

        //reward = Mathf.Clamp(reward, -1.0f, 1.0f);
        //AddReward(reward);
        //lastActualClearRate = actualClearRate;
        // =============================================================

        // [대조군 실험 2] 가우시안 보상 함수 + 허용 오차(Tolerance)
        // =============================================================
        float tolerance = 0.05f;
        float sigma = 0.15f;
        float reward = 0f;

        if (error <= tolerance)
        {
            reward = 1.0f; // 목표 달성 시 만점 부여
        }
        else
        {
            float adjustedError = error - tolerance;
            float gaussianValue = Mathf.Exp(-(adjustedError * adjustedError) / (2.0f * sigma * sigma));
            reward = (2.0f * gaussianValue) - 1.0f;
        }

        reward = Mathf.Clamp(reward, -1.0f, 1.0f);
        AddReward(reward);
        lastActualClearRate = actualClearRate;
        // =============================================================
#if UNITY_EDITOR
        float sumWeights = 0f;
        for (int i = 0; i < 4; i++) sumWeights += currentPatternWeights[i];

        float entropy = 0f;
        float[] p = new float[4];
        for (int i = 0; i < 4; i++)
        {
            p[i] = currentPatternWeights[i] / sumWeights;
            if (p[i] > 0)
            {
                entropy -= p[i] * Mathf.Log(p[i], 2f);
            }
        }

        // =====================================================
        Debug.Log($"[EDPCG 최종 테스트] 타겟: {targetClearRate*100:F0}% | 실제({episodesPerBatch}명 혼합): {actualClearRate*100:F0}% | 오차: {error*100:F1}% | 쿨타임: {currentCooldown:F2} | 보상: {reward:F2} | 엔트로피: {entropy:F3}");

        // [추가된 부분] 페르소나별 생존율 계산 및 콘솔 출력
        float beginnerSurvival = personaSpawnCounts[0] > 0 ? (float)personaClearCounts[0] / personaSpawnCounts[0] : 0f;
        float intermediateSurvival = personaSpawnCounts[1] > 0 ? (float)personaClearCounts[1] / personaSpawnCounts[1] : 0f;
        float expertSurvival = personaSpawnCounts[2] > 0 ? (float)personaClearCounts[2] / personaSpawnCounts[2] : 0f;

        Debug.Log($"[페르소나 분석] 초보 생존: {personaClearCounts[0]}/{personaSpawnCounts[0]} ({beginnerSurvival*100:F1}%) | 중수 생존: {personaClearCounts[1]}/{personaSpawnCounts[1]} ({intermediateSurvival*100:F1}%) | 고수 생존: {personaClearCounts[2]}/{personaSpawnCounts[2]} ({expertSurvival*100:F1}%)");

        string filePath = Application.dataPath + "/BossAnalysisData_Persona_50%.csv";
        if (!File.Exists(filePath))
        {
            string header = "TargetClearRate,ActualClearRate,ErrorRate,Cooldown,Entropy,Weight1,Weight2,Weight3,Weight4\n";
            File.WriteAllText(filePath, header);
        }
        string dataLine = $"{targetClearRate},{actualClearRate},{error},{currentCooldown},{entropy},{p[0]:F2},{p[1]:F2},{p[2]:F2},{p[3]:F2}\n";
        File.AppendAllText(filePath, dataLine);
        // =====================================================
#endif

        currentBatchCount++;

        if (currentBatchCount >= batchesPerBossEpisode)
        {
            player.EndEpisode();
            EndEpisode();
        }
        else
        {
            // 새로운 배치를 시작하기 전 첫 유저 셋팅
            SetupRandomPlayer();
            player.EndEpisode();
            StartNewBatch();
        }
    }
}