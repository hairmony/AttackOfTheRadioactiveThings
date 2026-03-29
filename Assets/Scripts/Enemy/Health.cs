using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float health = 10f;

    void Update()
    {
        if (health <= 0)
        {
            Destroy(gameObject);
            return;
        }
    }
    public void TakeDamage(float damage)
    {
        health -= damage;
    }
}
