using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections; // 코루틴용 추가

public class PlayerController : Agent
{
    public enum PersonaType { Beginner, Intermediate, Expert }
    [Header("Persona Settings")]
    public PersonaType currentPersona = PersonaType.Expert;

    [Header("Survival / Clear Settings")]
    public float targetSurvivalTime = 60f;
    private float currentSurvivalTime = 0f;
    private bool isEpisodeFinished = false;

    public float currentDPS;
    public float accumulatedDamage = 0f;
    public float bossMaxHealth = 500f;

    // ==========================================
    // 💡 새롭게 추가된 라이프(HP) 시스템
    // ==========================================
    [Header("Life System")]
    public int maxHP = 3;             // 최대 라이프 수
    public int currentHP;             // 현재 라이프 수

    [Header("Movement Settings")]
    public float moveSpeed = 2.5f;
    public float jumpForce = 5f;
    public float dashForce = 7.5f;
    public float dashDuration = 0.2f;
    private Vector3 startPos;

    [Header("State Check")]
    public bool isGrounded;
    public bool isDucking;
    public bool isDashing;
    private bool isInvincible = false;
    public float invincibilityDuration = 0.5f; // 피격 후 무적 시간

    [Header("Grounded Settings")]
    public LayerMask groundLayer;
    public float rayDistance = 0.1f;

    private int jumpCount = 0;
    private int maxJumps = 2;
    private bool isJumpCooldown = false;
    private float jumpCooldown = 0.2f;
    private float lastJumpTime;

    [Header("Dash Settings")]
    public int maxAirDashes = 1;
    private int airDashCount = 0;
    public float dashCooldown = 0.3f;
    private bool isDashCooldown = false;
    private float facingDir = 1f;

    private Rigidbody2D rb;
    private BoxCollider2D col;
    private Vector2 originalSize;
    private Vector2 originalOffset;
    private Vector3 originalScale;

    [Header("Boss & Environment References")]
    public Transform bossTransform;
    public BossPatternManager bossManager;
    // 1단계 패턴
    public GameObject highAttackZone;
    public GameObject lowAttackZone;
    // 2단계
    public GameObject dashAttackZone;
    public GameObject stunZone;
    public GameObject bossScoringZone;
    public float timeInStunZone = 0f;

    // ==========================================
    // 💡 3단계 궁극기 변수들 원상 복구 (에러 방지용)
    // ==========================================
    public GameObject[] ultimateZones = new GameObject[5];
    private float highTimer = 0f;
    private float lowTimer = 0f;
    private float dashTimer = 0f;
    private float stunTimer = 0f;
    private float[] ultTimers = new float[5];

    private bool jumpRequested = false;
    private bool dashRequested = false;
    private int prevJumpAction = 0;
    private int prevDashAction = 0;

    [Header("Reward Tracking")]
    private bool prevHighActive = false;
    private bool prevLowActive = false;
    private bool prevDashActive = false;
    private bool prevStunActive = false;
    private bool[] prevUltActive = new bool[5];

    public BossAgent bossAgent;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        col = GetComponent<BoxCollider2D>();
        originalSize = col.size;
        originalOffset = col.offset;
        originalScale = transform.localScale;
        startPos = transform.localPosition;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt))
            jumpRequested = true;

        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Q))
            dashRequested = true;
    }

    void FixedUpdate()
    {
        Vector2 rayStart = new Vector2(col.bounds.center.x, col.bounds.min.y + 0.05f);
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 0.15f, groundLayer);
        RaycastHit2D ceilingHit = Physics2D.Raycast(new Vector2(col.bounds.center.x, col.bounds.max.y), Vector2.up, 0.1f);

        if (ceilingHit.collider != null)
        {
            if (ceilingHit.collider.CompareTag("Ceiling"))
            {
                AddReward(-1.0f);
                TakeDamage("천장 충돌 즉사", true); // 즉사 플래그
            }
        }

        if (hit.collider != null)
        {
            if (hit.collider.CompareTag("Floor"))
            {
                isGrounded = true;
                jumpCount = 0;
                airDashCount = 0;
            }
        }
        else
        {
            isGrounded = false;
        }

        if (Mathf.Abs(transform.localPosition.x) > 20f || transform.localPosition.y < -10f)
        {
            AddReward(-1.0f);
            TakeDamage("장외 추락 즉사", true); // 즉사 플래그
        }

        bool isAnyUltActive = false;
        if (ultimateZones != null)
        {
            for (int i = 0; i < ultimateZones.Length; i++)
            {
                if (ultimateZones[i] != null && ultimateZones[i].activeSelf)
                {
                    isAnyUltActive = true;
                    break;
                }
            }
        }

        bool currentHighActive = highAttackZone != null && highAttackZone.activeSelf;
        if (prevHighActive && !currentHighActive) AddReward(1.0f);
        prevHighActive = currentHighActive;

        bool currentLowActive = lowAttackZone != null && lowAttackZone.activeSelf;
        if (prevLowActive && !currentLowActive) AddReward(1.0f);
        prevLowActive = currentLowActive;

        bool currentDashActive = dashAttackZone != null && dashAttackZone.activeSelf;
        if (prevDashActive && !currentDashActive) AddReward(1.0f);
        prevDashActive = currentDashActive;

        bool currentStunActive = stunZone != null && stunZone.activeSelf;
        if (prevStunActive && !currentStunActive) AddReward(1.0f);
        prevStunActive = currentStunActive;

        for (int i = 0; i < 5; i++)
        {
            bool currentUltActive = ultimateZones != null && i < ultimateZones.Length && ultimateZones[i] != null && ultimateZones[i].activeSelf;
            if (prevUltActive[i] && !currentUltActive) AddReward(1.0f);
            prevUltActive[i] = currentUltActive;
        }

        highTimer = (highAttackZone != null && highAttackZone.activeSelf) ? highTimer + Time.fixedDeltaTime : 0f;
        lowTimer = (lowAttackZone != null && lowAttackZone.activeSelf) ? lowTimer + Time.fixedDeltaTime : 0f;
        dashTimer = (dashAttackZone != null && dashAttackZone.activeSelf) ? dashTimer + Time.fixedDeltaTime : 0f;
        stunTimer = (stunZone != null && stunZone.activeSelf) ? stunTimer + Time.fixedDeltaTime : 0f;

        for (int i = 0; i < 5; i++)
        {
            if (ultimateZones != null && i < ultimateZones.Length && ultimateZones[i] != null && ultimateZones[i].activeSelf)
            {
                ultTimers[i] += Time.fixedDeltaTime;
                isAnyUltActive = true;
            }
            else
            {
                ultTimers[i] = 0f;
            }
        }

        if ((highAttackZone != null && highAttackZone.activeSelf) ||
             (lowAttackZone != null && lowAttackZone.activeSelf) ||
             (dashAttackZone != null && dashAttackZone.activeSelf) ||
             (stunZone != null && stunZone.activeSelf) ||
             isAnyUltActive)
        {
            AddReward(0.002f);
        }

        if (bossScoringZone != null && bossScoringZone.activeSelf)
        {
            // 빈 공간 유지
        }

        if (!isEpisodeFinished)
        {
            currentSurvivalTime += Time.fixedDeltaTime;

            if (accumulatedDamage >= bossMaxHealth)
            {
                isEpisodeFinished = true;
                if (bossAgent != null) bossAgent.ReportPlayerResult(true, accumulatedDamage);
                else EndEpisode();
            }
            else if (currentSurvivalTime >= targetSurvivalTime)
            {
                isEpisodeFinished = true;
                if (bossAgent != null) bossAgent.ReportPlayerResult(false, accumulatedDamage);
                else EndEpisode();
            }
        }
    }

    private float GetPerceptionThreshold()
    {
        switch (currentPersona)
        {
            case PersonaType.Expert: return 0.0f;
            case PersonaType.Intermediate: return 0.2f;
            case PersonaType.Beginner: return 0.5f;
            default: return 0.0f;
        }
    }

    void Duck(bool ducking)
    {
        if (isDucking == ducking) return;
        isDucking = ducking;

        if (ducking)
        {
            col.size = new Vector2(originalSize.x, originalSize.y * 0.5f);
            col.offset = new Vector2(originalOffset.x, originalOffset.y - (originalSize.y * 0.25f));
            transform.localScale = new Vector3(originalScale.x, originalScale.y * 0.5f, originalScale.z);
            transform.position = new Vector3(transform.position.x, transform.position.y - (originalScale.y * 0.25f), transform.position.z);
        }
        else
        {
            col.size = originalSize;
            col.offset = originalOffset;
            transform.localScale = originalScale;
            transform.position = new Vector3(transform.position.x, transform.position.y + (originalScale.y * 0.25f), transform.position.z);
        }
    }

    IEnumerator JumpCooldownRoutine()
    {
        isJumpCooldown = true;
        yield return new WaitForSeconds(jumpCooldown);
        isJumpCooldown = false;
    }

    IEnumerator EnhancedDashRoutine()
    {
        isDashing = true;
        float originalGravity = rb.gravityScale;

        rb.gravityScale = 0f;
        rb.velocity = new Vector2(facingDir * dashForce, 0f);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        isDashing = false;

        isDashCooldown = true;
        yield return new WaitForSeconds(dashCooldown);
        isDashCooldown = false;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            isGrounded = false;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("HighAttack")) { if (!isDucking) TakeDamage("상단 공격"); }
        else if (collision.CompareTag("LowAttack")) { if (isGrounded) TakeDamage("하단 공격"); }
        else if (collision.CompareTag("DashAttack")) { TakeDamage("대쉬 타겟팅 공격"); }
        else if (collision.CompareTag("StunAttack")) { TakeDamage("근접 기절 공격"); }
        else if (collision.CompareTag("ScoringZone"))
        {
            AddReward(0.005f);
            accumulatedDamage += currentDPS * Time.fixedDeltaTime;
        }
    }

    // ==========================================
    // 💡 HP 적용 및 무적 코루틴을 위한 피격 로직 수정
    // ==========================================
    void TakeDamage(string attackType, bool instantKill = false)
    {
        if (isInvincible || isEpisodeFinished) return;

        // 즉사 패턴(낙사 등)이면 남은 체력 상관없이 0으로, 아니면 체력 1 차감
        if (instantKill) currentHP = 0;
        else currentHP--;

        // 체력이 다 떨어졌다면 사망 처리
        if (currentHP <= 0)
        {
            isInvincible = true;
            isEpisodeFinished = true;

            Debug.Log($"[사망] 플레이어가 '{attackType}'에 의해 즉사했습니다!");
            FindObjectOfType<QAAnalyzer>()?.OnPlayerHit();
            AddReward(-1.0f);

            // 기존 로직 유지 (안전빵)
            Invoke("ResetInvincibility", invincibilityDuration);

            if (bossAgent != null)
            {
                bossAgent.ReportPlayerResult(false, accumulatedDamage);
            }
            else
            {
                EndEpisode();
            }
        }
        else
        {
            // 체력이 남았다면 무적 시간 돌입!
            Debug.Log($"[피격] '{attackType}'에 맞았습니다! 남은 체력: {currentHP}");
            StartCoroutine(InvincibilityRoutine());
        }
    }

    // 피격 후 무적 시간 코루틴
    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    private void ResetInvincibility()
    {
        isInvincible = false;
    }

    // ===== ML-AGENTS =====
    public override void OnEpisodeBegin()
    {
        currentSurvivalTime = 0f;
        isEpisodeFinished = false;
        accumulatedDamage = 0f;

        // 💡 에피소드 시작 시 체력 원상 복구
        currentHP = maxHP;
        isInvincible = false;

        transform.localPosition = startPos;
        rb.velocity = Vector2.zero;
        jumpCount = 0;
        isGrounded = true;
        isDashing = false;
        Duck(false);
        timeInStunZone = 0f;
        rb.gravityScale = 1.25f;

        highTimer = lowTimer = dashTimer = stunTimer = 0f;
        for (int i = 0; i < 5; i++) ultTimers[i] = 0f;

        prevHighActive = prevLowActive = prevDashActive = prevStunActive = false;
        for (int i = 0; i < 5; i++) prevUltActive[i] = false;

        if (highAttackZone != null) highAttackZone.SetActive(false);
        if (lowAttackZone != null) lowAttackZone.SetActive(false);
        if (dashAttackZone != null) dashAttackZone.SetActive(false);
        if (stunZone != null) stunZone.SetActive(false);

        if (ultimateZones != null)
        {
            foreach (var zone in ultimateZones)
            {
                if (zone != null) zone.SetActive(false);
            }
        }

        if (bossManager != null)
        {
            bossManager.ResetAllPatterns();
        }
    }

    // ==========================================
    // 💡 관측값 40개 완벽하게 원상 복구 완료
    // ==========================================
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition.x);
        sensor.AddObservation(transform.localPosition.y);

        sensor.AddObservation(bossTransform != null ? bossTransform.localPosition.x : 0f);
        sensor.AddObservation(bossTransform != null ? bossTransform.localPosition.y : 0f);

        float threshold = GetPerceptionThreshold();

        bool isHighActive = highAttackZone != null && highAttackZone.activeSelf;
        bool isHighAttacking = isHighActive && highAttackZone.GetComponent<Collider2D>().enabled;
        float highNormalized = isHighActive ? Mathf.Clamp01(highTimer / 0.5f) : 0.0f;
        if (highNormalized < threshold) highNormalized = 0.0f;
        sensor.AddObservation(highNormalized);
        sensor.AddObservation(isHighAttacking ? 1.0f : 0.0f);

        bool isLowActive = lowAttackZone != null && lowAttackZone.activeSelf;
        bool isLowAttacking = isLowActive && lowAttackZone.GetComponent<Collider2D>().enabled;
        float lowNormalized = isLowActive ? Mathf.Clamp01(lowTimer / 0.5f) : 0.0f;
        if (lowNormalized < threshold) lowNormalized = 0.0f;
        sensor.AddObservation(lowNormalized);
        sensor.AddObservation(isLowAttacking ? 1.0f : 0.0f);

        bool isScoringActive = bossScoringZone != null && bossScoringZone.activeSelf;
        sensor.AddObservation(isScoringActive ? 1.0f : 0.0f);
        sensor.AddObservation(bossScoringZone != null ? bossScoringZone.transform.localPosition.x : 0f);
        sensor.AddObservation(bossScoringZone != null ? bossScoringZone.transform.localPosition.y : 0f);

        bool isDashActive = dashAttackZone != null && dashAttackZone.activeSelf;
        bool isDashAttacking = isDashActive && dashAttackZone.GetComponent<Collider2D>() != null && dashAttackZone.GetComponent<Collider2D>().enabled;
        float dashNormalized = isDashActive ? Mathf.Clamp01(dashTimer / 0.5f) : 0.0f;
        if (dashNormalized < threshold) dashNormalized = 0.0f;
        sensor.AddObservation(dashNormalized);
        sensor.AddObservation(isDashAttacking ? 1.0f : 0.0f);
        sensor.AddObservation(dashAttackZone != null ? dashAttackZone.transform.localPosition.x : 0f);
        sensor.AddObservation(dashAttackZone != null ? dashAttackZone.transform.localPosition.y : 0f);

        bool isStunActive = stunZone != null && stunZone.activeSelf;
        bool isStunAttacking = isStunActive && stunZone.GetComponent<Collider2D>() != null && stunZone.GetComponent<Collider2D>().enabled;
        float stunNormalized = isStunActive ? Mathf.Clamp01(stunTimer / 0.5f) : 0.0f;
        if (stunNormalized < threshold) stunNormalized = 0.0f;
        sensor.AddObservation(stunNormalized);
        sensor.AddObservation(isStunAttacking ? 1.0f : 0.0f);
        sensor.AddObservation(stunZone != null ? stunZone.transform.localPosition.x : 0f);
        sensor.AddObservation(stunZone != null ? stunZone.transform.localPosition.y : 0f);

        bool isUltActive = ultimateZones.Length > 0 && ultimateZones[0] != null && ultimateZones[0].activeSelf;
        float ultNormalized = isUltActive ? Mathf.Clamp01(ultTimers[0] / 0.5f) : 0.0f;
        if (ultNormalized < threshold) ultNormalized = 0.0f;
        sensor.AddObservation(ultNormalized);

        for (int i = 0; i < 5; i++)
        {
            if (i < ultimateZones.Length && ultimateZones[i] != null && ultimateZones[i].activeSelf)
            {
                var ultScript = ultimateZones[i].GetComponent<UltimateLineAttack>();
                sensor.AddObservation(ultScript.startPoint.x);
                sensor.AddObservation(ultScript.startPoint.y);
                sensor.AddObservation(ultScript.endPoint.x);
                sensor.AddObservation(ultScript.endPoint.y);
            }
            else
            {
                sensor.AddObservation(0f); sensor.AddObservation(0f);
                sensor.AddObservation(0f); sensor.AddObservation(0f);
            }
        }
        // 총 확보된 Observation Size: 40 (변경 없음!)
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isDashing) return;

        int moveAction = actions.DiscreteActions[0];
        int jumpAction = actions.DiscreteActions[1];
        int duckAction = actions.DiscreteActions[2];
        int dashAction = actions.DiscreteActions[3];

        float rand = Random.value;

        if (currentPersona == PersonaType.Beginner)
        {
            if (rand < 0.1f)
            {
                dashAction = (Random.value > 0.5f) ? 1 : 0;
                moveAction = Random.Range(0, 3);
            }
            else if (rand < 0.15f)
            {
                jumpAction = 0;
                duckAction = 0;
            }
        }
        else if (currentPersona == PersonaType.Intermediate)
        {
            if (rand < 0.05f)
            {
                jumpAction = 0;
                duckAction = 0;
            }
        }

        float moveInput = 0f;
        if (moveAction == 1) moveInput = -1f;
        else if (moveAction == 2) moveInput = 1f;

        float currentMoveSpeed = isGrounded ? moveSpeed : moveSpeed * 0.7f;
        rb.velocity = new Vector2(moveInput * currentMoveSpeed, rb.velocity.y);
        if (moveInput != 0) facingDir = moveInput;

        if (jumpAction == 1 && prevJumpAction == 0)
        {
            if (jumpCount < maxJumps && !isJumpCooldown)
            {
                rb.velocity = new Vector2(rb.velocity.x, 0);
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                jumpCount++;
                StartCoroutine(JumpCooldownRoutine());
            }
        }

        if (duckAction == 1 && isGrounded)
        { Duck(true); }
        else { Duck(false); }

        if (dashAction == 1 && prevDashAction == 0)
        {
            if (!isDashing && !isDashCooldown)
            {
                bool canDash = false;
                if (isGrounded) canDash = true;
                else if (airDashCount < maxAirDashes)
                {
                    canDash = true;
                    airDashCount++;
                }

                if (canDash) StartCoroutine(EnhancedDashRoutine());
            }
        }

        prevJumpAction = jumpAction;
        prevDashAction = dashAction;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        if (Input.GetAxisRaw("Horizontal") < 0) discreteActions[0] = 1;
        else if (Input.GetAxisRaw("Horizontal") > 0) discreteActions[0] = 2;
        else discreteActions[0] = 0;

        if (jumpRequested)
        {
            discreteActions[1] = 1;
            jumpRequested = false;
        }
        else
        {
            discreteActions[1] = 0;
        }

        if (Input.GetKey(KeyCode.DownArrow)) discreteActions[2] = 1;
        else discreteActions[2] = 0;

        if (dashRequested)
        {
            discreteActions[3] = 1;
            dashRequested = false;
        }
        else
        {
            discreteActions[3] = 0;
        }
    }
}