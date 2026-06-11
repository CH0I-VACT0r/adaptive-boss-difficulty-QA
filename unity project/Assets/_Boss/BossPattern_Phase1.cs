using UnityEngine;
using System.Collections;

public class BossPattern_Phase1 : MonoBehaviour
{
    [Header("Attack Settings")]
    public float warningDuration = 0.5f; // 빨간색 경고가 보여지는 시간
    public float attackDuration = 0.25f;  // 실제 공격 판정이 남아있는 시간

    [Header("Warning/Attack Zones (Assign in Inspector)")]
    public GameObject highAttackZone; // 상단 공격 범위 (서있으면 맞음)
    public GameObject lowAttackZone;  // 하단 공격 범위 (땅에 있으면 맞음)

    void Start()
    {
        highAttackZone.SetActive(false);
        lowAttackZone.SetActive(false);
    }

    // 상단 공격 (숙여서 피해야 함)
    public void ExecuteHighAttack(BossPatternManager manager)
    {
        StartCoroutine(AttackRoutine(highAttackZone, manager));
    }
    // 하단 공격 (점프로 피해야 함)
    public void ExecuteLowAttack(BossPatternManager manager)
    {
        StartCoroutine(AttackRoutine(lowAttackZone, manager));
    }

    IEnumerator AttackRoutine(GameObject attackZone, BossPatternManager manager)
    {
        FindObjectOfType<QAAnalyzer>()?.OnAttackStarted();

        manager.isAttacking = true;
        attackZone.SetActive(true);

        Collider2D col = attackZone.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        SpriteRenderer sr = attackZone.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0f, 0f, 0.3f);

        yield return new WaitForSeconds(warningDuration);
        if (sr != null) sr.color = new Color(1f, 0f, 0f, 1f);
        if (col != null) col.enabled = true;
        yield return new WaitForSeconds(attackDuration);

        attackZone.SetActive(false);
        if (col != null) col.enabled = false;
        manager.isAttacking = false;
    }

    public void ResetBoss()
    {
        StopAllCoroutines(); // 진행 중인 모든 공격 코루틴 중지

        // 장판 끄기
        if (highAttackZone != null) highAttackZone.SetActive(false);
        if (lowAttackZone != null) lowAttackZone.SetActive(false);

        // 혹시 켜져 있을지 모르는 콜라이더 안전하게 끄기
        if (highAttackZone.TryGetComponent<Collider2D>(out var col1)) col1.enabled = false;
        if (lowAttackZone.TryGetComponent<Collider2D>(out var col2)) col2.enabled = false;

        // BossPatternManager가 있다면 상태도 초기화 (스크립트 구조에 맞춰 적용)
        var manager = GetComponent<BossPatternManager>();
        if (manager != null) manager.isAttacking = false;
    }
}
