using UnityEngine;
using Unity.MLAgents; // 나중에 ML-Agents 연결할 때 주석 해제

public class BossPatternManager : MonoBehaviour
{
    [Header("Curriculum Settings")]
    // 1: 점프/숙이기, 2: 대쉬/거리유지, 3: 궁극기(특정 좌표)
    public int currentLesson = 1;

    [Header("Pattern Scripts")]
    public BossPattern_Phase1 phase1Pattern;
    public BossPattern_Phase2 phase2Pattern;
    // public BossPattern_Phase3 phase3Pattern; // 나중에 추가

    [Header("Movement Settings")]
    public Transform player;       // 쫓아갈 플레이어 지정
    public float moveSpeed = 0.5f; // 보스의 이동 속도 (느리게 설정)
    private float startY = -3.5f;
    public bool isAttacking = false; // 공격 중인지 체크

    private float patternTimer = 0f;
    public float patternCooldown = 3f; // 패턴 사이의 간격

    private float[] patternWeights = { 1f, 1f, 1f, 1f };

    void Start()
    {
        startY = transform.position.y;

    }
    void Update()
    {
        // 나중에 ML-Agents의 Academy.Instance.EnvironmentParameters.GetWithDefault("lesson_level", 1); 
        // 등을 통해 currentLesson 값을 동적으로 업데이트하게 됩니다.

        if (player != null && !isAttacking)
        {
            Vector3 targetPosition = new Vector3(player.position.x, startY, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }

        patternTimer += Time.deltaTime;

        if (patternTimer >= patternCooldown)
        {
            ExecuteRandomPatternForCurrentLesson();
            patternTimer = 0f; // 타이머 초기화
        }
    }

    void ExecuteRandomPatternForCurrentLesson()
    {
        switch (currentLesson)
        {
            case 1:
                bool isHighAttack = Random.value > 0.5f;
                if (isHighAttack) phase1Pattern.ExecuteHighAttack(this);
                else phase1Pattern.ExecuteLowAttack(this);
                break;

            case 2:
                int randomPattern = ChoosePatternByWeight();
                if (randomPattern == 0) phase1Pattern.ExecuteHighAttack(this);
                else if (randomPattern == 1) phase1Pattern.ExecuteLowAttack(this);
                else if (randomPattern == 2) phase2Pattern.ExecuteDashAttack(player, this);
                else phase2Pattern.ExecuteStunZone(this);
                break;

            case 3:
                // 3단계 로직 (궁극기)
                break;
        }
    }

    public void ResetAllPatterns()
    {
        isAttacking = false;
        if (phase1Pattern != null) phase1Pattern.ResetBoss(); // 기존 Phase1 리셋
        if (phase2Pattern != null) phase2Pattern.ResetPhase2(); // Phase2 리셋
    }

    public void UpdatePatternWeights(float[] newWeights)
    {
        patternWeights = newWeights;
    }

    private int ChoosePatternByWeight()
    {
        float totalWeight = 0f;
        foreach (float w in patternWeights) totalWeight += w;

        float randomVal = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < patternWeights.Length; i++)
        {
            cumulative += patternWeights[i];
            if (randomVal <= cumulative)
            {
                return i; // 0:상단, 1:하단, 2:대쉬, 3:기절
            }
        }
        return 0; // 혹시나 에러 나면 기본값
    }
}
