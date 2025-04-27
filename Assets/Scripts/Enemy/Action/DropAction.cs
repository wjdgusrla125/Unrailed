using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Netcode;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Drop", story: "[Agent] Drop at current tile", category: "Action", id: "8059f6157e25817690412be78c12aef4")]
public partial class DropAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;

    private AIBlockPickup aiBlockPickup;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
            return Status.Failure;

        aiBlockPickup = Agent.Value.GetComponent<AIBlockPickup>();

        if (aiBlockPickup == null)
        {
            Debug.LogError("[DropAction] AIBlockPickup 컴포넌트가 없습니다.");
            return Status.Failure;
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        aiBlockPickup.DetectTileBelow();
        aiBlockPickup.TryDropItem();
        return Status.Success;
    }
}