using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "CheckTargetDetect", story: "Compare values of [CurrentDistance] and [CheckDistance]", category: "Conditions", id: "1f32b3680170fd355e08630fe1dd072b")]
public partial class CheckTargetDetectCondition : Condition
{
    [SerializeReference] public BlackboardVariable<float> CurrentDistance;
    [SerializeReference] public BlackboardVariable<float> CheckDistance;

    public override bool IsTrue()
    {
        if (CurrentDistance.Value <= CheckDistance.Value)
        {
            return true;
        }
        return false;
    }
}
