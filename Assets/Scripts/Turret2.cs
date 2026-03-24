using UnityEngine;

public class Turret2 : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Attributes")]
    [SerializeField] private float range = 3f;
    [SerializeField] private float attackTime = 0.5f;
    [SerializeField] private float chargeTime = 2.5f;
    [SerializeField] private int maxCharges = 6;

    private Transform target;
    private float attackCooldown = 0;
    private int currentCharges = 0;
    private float chargeProgress = 0;

    private void Update()
    {
        if (target == null)
        {
            FindTarget();
        }
        else if (Vector2.Distance(target.position, transform.position) > range)
        {
            FindTarget();
        }
        else if (attackCooldown >= attackTime && currentCharges > 0)
        {
            Attack();
            attackCooldown = 0;
            currentCharges--;
        }

        if (attackCooldown < attackTime)
        {
            attackCooldown += Time.deltaTime;
        }

        // FIX 5: guard against exceeding maxCharges
        if (chargeProgress >= chargeTime)
        {
            if (currentCharges < maxCharges)
            {
                currentCharges++;
            }
            chargeProgress = 0;
        }
        else if (currentCharges < maxCharges)
        {
            chargeProgress += Time.deltaTime;
        }
    }

    private void Attack()
    {
        GameObject bulletInstance = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Bullet bulletScript = bulletInstance.GetComponent<Bullet>();
        bulletScript.SetTarget(target.gameObject);
        Debug.Log("Attacked");
    }

    private void FindTarget()
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, range, (Vector2)transform.position, 0f, enemyMask);

        if (hits.Length > 0)
        {
            target = hits[0].transform;
        }
        else
        {
            target = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);
        UnityEditor.Handles.DrawWireDisc(transform.position, transform.forward, range);
    }
#endif
}