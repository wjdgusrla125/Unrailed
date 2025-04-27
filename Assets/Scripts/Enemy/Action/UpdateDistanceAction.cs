using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "UpdateDistance", story: "Update [Self] and [Target] [currentDistance]", category: "Action", id: "8fa3484216bbadaa8570a26cc4cce806")]
public partial class UpdateDistanceAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> CurrentDistance;
    
    protected override Status OnUpdate()
    {
        CurrentDistance.Value = Vector3.Distance(Self.Value.transform.position, Target.Value.transform.position);
        
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

