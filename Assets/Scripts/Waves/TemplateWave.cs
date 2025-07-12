using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;

[CreateAssetMenu(fileName = "RatMischief", menuName = "Waves/Template")]
public class RatMischief : BaseWave
{
    [Tooltip("Tooltip")]
    [SerializeField] private int waveOccurences = 1;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        for (int i = 0; i < waveOccurences; i++)
        {
            
        }
        yield return new WaitForSeconds(1f);
    }
}