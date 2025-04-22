using System;
using Unity.Netcode;
using UnityEngine;

public class DeskInfo : NetworkBehaviour
{
    private NetworkVariable<int> railCount = new NetworkVariable<int>(0);
    private Animator animator;
    
    [SerializeField] private GameObject RailPrefab;
    
    public int RailCount
    {
        get { return railCount.Value; }
        set 
        { 
            if (IsServer)
                railCount.Value = value;
        }
    }
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public GameObject GetRailObject()
    {
        return RailPrefab;
    }

    public void GetRail()
    {
        if (IsServer)
        {
            if (railCount.Value > 0)
            {
                railCount.Value--;
                // 레일을 가져갔을 때 애니메이션 파라미터 초기화
                ResetRailAnimationClientRpc();
            }
        }
        else
        {
            GetRailServerRpc();
        }
    }

    public void RailCreateDone()
    {
        if (IsServer)
        {
            RailCreateDoneInternal();
        }
        else
        {
            RailCreateDoneServerRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RailCreateDoneServerRpc()
    {
        RailCreateDoneInternal();
    }

    private void RailCreateDoneInternal()
    {
        // 애니메이션 단계를 목표치(레일 개수)까지 올림
        int currentStep = animator.GetInteger("GetRails");
    
        // 현재 단계가 목표(레일 개수)보다 작으면 다음 단계로 진행
        if (currentStep < railCount.Value)
        {
            UpdateRailAnimationStepClientRpc(currentStep + 1);
        }
    }
    
    [ClientRpc]
    private void UpdateRailAnimationStepClientRpc(int step)
    {
        // 애니메이션 단계 업데이트
        if (animator != null)
        {
            animator.SetInteger("GetRails", step);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void GetRailServerRpc()
    {
        if (railCount.Value > 0)
        {
            railCount.Value--;
            // 레일을 가져갔을 때 애니메이션 파라미터 초기화
            ResetRailAnimationClientRpc();
        }
    }

    [ClientRpc]
    private void ResetRailAnimationClientRpc()
    {
        // 애니메이터의 GetRails 값을 0으로 초기화
        if (animator != null)
        {
            animator.SetInteger("GetRails", 0);
        }
    }
    
    public void UpdateRailAnimation(int step)
    {
        if (IsServer)
        {
            UpdateRailAnimationClientRpc(step);
        }
    }

    [ClientRpc]
    private void UpdateRailAnimationClientRpc(int step)
    {
        // 애니메이션 단계 업데이트
        if (animator != null)
        {
            animator.SetInteger("GetRails", step);
        }
    }
}