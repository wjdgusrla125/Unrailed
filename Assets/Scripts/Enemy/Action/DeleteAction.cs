using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Unity.Netcode;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Delete", story: "[Agent] Delete Held Item", category: "Action", id: "6f036f7f2f9f2238b9fc5353632b6f30")]
public partial class DeleteAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;

    private AIBlockPickup aiBlockPickup;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("[DeleteAction] Agent가 설정되지 않았습니다.");
            return Status.Failure;
        }

        aiBlockPickup = Agent.Value.GetComponent<AIBlockPickup>();

        if (aiBlockPickup == null)
        {
            Debug.LogError("[DeleteAction] Agent에 AIBlockPickup 컴포넌트가 없습니다.");
            return Status.Failure;
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (aiBlockPickup == null)
            return Status.Failure;

        var heldObject = aiBlockPickup.MainHeldObject;
        if (heldObject == null)
        {
            Debug.LogWarning("[DeleteAction] 들고 있는 오브젝트가 없습니다.");
            return Status.Failure;
        }

        if (heldObject.IsSpawned)
        {
            heldObject.Despawn();
            Debug.Log("[DeleteAction] 들고 있는 오브젝트를 삭제했습니다.");
        }

        if (aiBlockPickup.heldObjectStack.Count > 0)
        {
            aiBlockPickup.heldObjectStack.Pop();
        }

        return Status.Success;
    }

    protected override void OnEnd()
    {
        // 특별한 정리 필요 없음
    }
}