using System;
using Unity.Behavior;

[BlackboardEnum]
public enum EnemyState
{
    Idle,
	Detect,
	Root,
	Escape,
	Dead
}
