
using System;
using Unity.Netcode;
using UnityEngine;

public class Bolt: NetworkBehaviour
{
    [SerializeField] private GameObject collectEffect;
    [SerializeField] private GameObject bolt;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (NetworkManager.Singleton.IsHost)
            {
                CollectBoltRpc(NetworkObjectId);
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void CollectBoltRpc(ulong id)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj))
        {
            obj.GetComponent<Collider>().enabled = false;
            Bolt boltObj = obj.GetComponent<Bolt>();
            boltObj.bolt.SetActive(false);
            boltObj.collectEffect.SetActive(true);
            GameManager.Instance.Bolt++;
        }
    }
}
