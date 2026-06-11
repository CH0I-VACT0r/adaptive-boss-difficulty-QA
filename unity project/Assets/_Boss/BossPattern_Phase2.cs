using UnityEngine;
using System.Collections;

public class BossPattern_Phase2 : MonoBehaviour
{
    [Header("Attack Settings")]
    public float warningDuration = 0.5f; // 경고 시간
    public float attackDuration = 0.25f; // 실제 타격 시간

    [Header("Warning/Attack Zones (Assign in Inspector)")]
    public GameObject dashAttackZone; // 플레이어 위치를 타겟팅하는 장판
    public GameObject stunZone;       // 보스 주변 기절(광역) 장판

    void Start()
    {
        if (dashAttackZone != null) dashAttackZone.SetActive(false);
        if (stunZone != null) stunZone.SetActive(false);
    }

    // 1. 대쉬 유도 공격 (플레이어 마지막 위치 타겟팅)
    public void ExecuteDashAttack(Transform playerTransform, BossPatternManager manager)
    {
        StartCoroutine(DashAttackRoutine(playerTransform, manager));
    }

    // 2. 근접 기절 장판 (보스 주변)
    public void ExecuteStunZone(BossPatternManager manager)
    {
        StartCoroutine(StunZoneRoutine(manager));
    }

    IEnumerator DashAttackRoutine(Transform player, BossPatternManager manager)
    {
        FindObjectOfType<QAAnalyzer>()?.OnAttackStarted();
        manager.isAttacking = true;

        // 1. 플레이어의 현재 X 위치를 캡처하여 장판 이동
        Vector3 targetPos = new Vector3(player.position.x, player.position.y, dashAttackZone.transform.position.z);
        dashAttackZone.transform.position = targetPos;
        dashAttackZone.SetActive(true);

        Collider2D col = dashAttackZone.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        SpriteRenderer sr = dashAttackZone.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0f, 0f, 0.3f);

        yield return new WaitForSeconds(warningDuration);

        // 2. 공격 판정 켜기
        if (sr != null) sr.color = new Color(1f, 0f, 0f, 1f);
        if (col != null) col.enabled = true;

        yield return new WaitForSeconds(attackDuration);

        // 3. 종료
        dashAttackZone.SetActive(false);
        if (col != null) col.enabled = false;
        manager.isAttacking = false;
    }

    IEnumerator StunZoneRoutine(BossPatternManager manager)
    {
        FindObjectOfType<QAAnalyzer>()?.OnAttackStarted();
        manager.isAttacking = true;

        stunZone.SetActive(true);

        Collider2D col = stunZone.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        SpriteRenderer sr = stunZone.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0f, 0f, 0.3f);

        yield return new WaitForSeconds(warningDuration);

        // 공격 판정 켜기
        if (sr != null) sr.color = new Color(1f, 0f, 0f, 1f);
        if (col != null) col.enabled = true;

        yield return new WaitForSeconds(attackDuration);

        stunZone.SetActive(false);
        if (col != null) col.enabled = false;
        manager.isAttacking = false;
    }

    public void ResetPhase2()
    {
        StopAllCoroutines();
        if (dashAttackZone != null) dashAttackZone.SetActive(false);
        if (stunZone != null) stunZone.SetActive(false);
        if (dashAttackZone != null && dashAttackZone.TryGetComponent<Collider2D>(out var col1)) col1.enabled = false;
        if (stunZone != null && stunZone.TryGetComponent<Collider2D>(out var col2)) col2.enabled = false;
    }
}