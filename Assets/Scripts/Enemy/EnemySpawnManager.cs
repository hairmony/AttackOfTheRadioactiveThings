using System.Collections;
using UnityEngine;

public class EnemySpawnManager : MonoBehaviour
{
    public GameObject Enemy;
    public Transform[] spawnPts;

    [SerializeField]
    private float spawnDelay = 5f;
    [SerializeField]
    private int spawnLimit = 0;
    [SerializeField]
    private bool stopSpawn = false;

    private int spawnCount = 0;

    void Start()
    {
        StartCoroutine(SpawnEnemies());
    }

    IEnumerator SpawnEnemies()
    {
        Vector3 offset = new(0.5f, 0.5f, 0f);
        while (!stopSpawn)
        {
            yield return new WaitForSeconds(spawnDelay);
            if (spawnCount >= spawnLimit)
            {
                stopSpawn = true;
                continue;
            }

            int randInt = Random.Range(0, spawnPts.Length);
            Transform spawnPt = spawnPts[randInt]; //Array size refers to total number of points present in the scene

            Instantiate(Enemy, spawnPt.position + offset, spawnPt.rotation); //offset is used to reduce complexity of EnemyPathing implementation
            spawnCount++;
        }
    }
}
