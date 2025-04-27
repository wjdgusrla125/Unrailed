using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Pickup", story: "[Agent] Pickup at [Target]", category: "Action", id: "0d279b649f3cecb4b5c57a8ee7bb9528")]
public partial class PickupAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    private AIBlockPickup aiBlockPickup;
    private Tile detectedTile;
    private LayerMask tileLayerMask;
    private float detectionRadius = 0.8f;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null || Target == null || Target.Value == null)
            return Status.Failure;

        aiBlockPickup = Agent.Value.GetComponent<AIBlockPickup>();

        if (aiBlockPickup == null)
        {
            Debug.LogError("PickupAction: AIBlockPickup 컴포넌트가 없습니다.");
            return Status.Failure;
        }

        tileLayerMask = aiBlockPickup.tileLayerMask;

        detectedTile = DetectTileAtTarget();
        if (detectedTile == null || detectedTile.GetStackSize() == 0)
        {
            Debug.LogWarning("PickupAction: 타겟 위치에 아이템이 없음");
            return Status.Failure;
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        aiBlockPickup.DetectTileBelow();
        aiBlockPickup.TryPickupItem();
        return Status.Success;
    }

    private Tile DetectTileAtTarget()
    {
        Vector3 targetPosition = Target.Value.transform.position;
        Collider[] tileColliders = Physics.OverlapSphere(targetPosition, detectionRadius, tileLayerMask);

        if (tileColliders.Length > 0)
        {
            float closestDistance = float.MaxValue;
            Tile closestTile = null;
            foreach (var collider in tileColliders)
            {
                Tile tile = collider.GetComponent<Tile>();
                if (tile != null)
                {
                    float distance = Vector3.Distance(targetPosition, tile.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTile = tile;
                    }
                }
            }
            return closestTile;
        }

        return null;
    }
}
