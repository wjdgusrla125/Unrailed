using System;
using Unity.Netcode;
using UnityEngine;

public class DeskInfo : NetworkBehaviour
{
    private NetworkVariable<int> railCount = new NetworkVariable<int>(0);
    
    [HideInInspector]
    public bool CanCreateRail = true;
    [Header("레일 오브젝트")]
    [SerializeField] private GameObject RailObject;

    public event Action CreateDoneRail;
    
    public int RailCount
    {
        get { return railCount.Value; }
        set 
        { 
            if (IsServer)
                railCount.Value = value;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 레일 카운트 변경 이벤트 등록
        railCount.OnValueChanged += OnRailCountChanged;
        
        // 클라이언트가 처음 접속했을 때 상태 요청
        if (IsClient && !IsServer)
            RequestInitialStateServerRpc();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // 이벤트 구독 해제
        railCount.OnValueChanged -= OnRailCountChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInitialStateServerRpc(ServerRpcParams rpcParams = default)
    {
        SyncInitialStateClientRpc(railCount.Value, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId }
            }
        });
    }

    [ClientRpc]
    private void SyncInitialStateClientRpc(int count, ClientRpcParams rpcParams = default)
    {
        // 애니메이션 상태 설정
        GetComponent<Animator>().SetInteger("GetRails", count);
    }

    private void OnRailCountChanged(int previousValue, int newValue)
    {
        // 레일 카운트가 변경될 때 애니메이션 업데이트
        GetComponent<Animator>().SetInteger("GetRails", newValue);
        
        // 클라이언트에서도 CanCreateRail 상태 업데이트
        RailCountCheck();
    }

    public void RailCreateDone()
    {
        if (IsServer)
        {
            // 레일 카운트 증가 (네트워크 변수)
            railCount.Value++;
            
            // 이벤트 호출
            CreateDoneRail?.Invoke();
        }
        else
        {
            // 클라이언트에서 서버에 레일 생성 완료 요청
            RailCreateDoneServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RailCreateDoneServerRpc()
    {
        // 레일 카운트 증가 (네트워크 변수)
        railCount.Value++;
        
        // 이벤트 호출
        CreateDoneRail?.Invoke();
    }

    public void RailCountCheck()
    {
        if (railCount.Value == 3)
            CanCreateRail = false;
        else
            CanCreateRail = true;
    }

    public void GetRail()
    {
        if (IsServer)
        {
            if (railCount.Value > 0)
            {
                railCount.Value--;
            }
        }
        else
        {
            // 클라이언트에서 레일 가져가기 요청
            GetRailServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void GetRailServerRpc()
    {
        if (railCount.Value > 0)
        {
            railCount.Value--;
        }
    }

    public GameObject GetRailObject()
    {
        return RailObject;
    }

    public void Update()
    {
        RailCountCheck();
    }
}