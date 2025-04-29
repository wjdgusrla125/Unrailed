
using System;
using Unity.Netcode;
using UnityEngine;

public class Bolt: NetworkBehaviour
{
    [SerializeField] private GameObject collectEffect;
    [SerializeField] private GameObject bolt;
    [SerializeField] private GameObject boltMesh;
    
    [SerializeField] private float spinSpeed = 43f;
    private bool _isSpinning;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        StartSpin();
    }

    private void Update()
    {
        if (_isSpinning && boltMesh)
        {
            // Y축(위 방향)으로 회전
            boltMesh.transform.Rotate(Vector3.down * (spinSpeed * Time.deltaTime), Space.Self);
        }
    }

    private void StartSpin()
    {
        _isSpinning = true;
    }

    private void StopSpin()
    {
        _isSpinning = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && NetworkManager.Singleton.IsHost)
        {
            CollectBoltRpc(NetworkObjectId);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void CollectBoltRpc(ulong id)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj))
        {
            StopSpin();
            obj.GetComponent<Collider>().enabled = false;
            Bolt boltObj = obj.GetComponent<Bolt>();
            boltObj.bolt.SetActive(false);
            boltObj.collectEffect.SetActive(true);
            GameManager.Instance.Bolt++;
        }
    }
}
