using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "IsNull", story: "[Agent] is null", category: "Conditions", id: "93c139fe05e6943487cfa7089e63f06f")]
public partial class IsNullCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;

    public override bool IsTrue()
    {
        return Agent == null || Agent.Value == null;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}