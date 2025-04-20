using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static Controls;

[CreateAssetMenu(fileName = "inputReader", menuName = "Input/InputReader")]
public class InputReader : ScriptableObject, IPlayerActions
{
    public event Action<Vector2> MoveEvent;
    public event Action<bool> InteractEvent;
    public event Action<bool> DashEvent;

    private Controls controls;
    private bool isInteractPressed = false; // Interact 입력 상태를 추적하는 변수

    private void OnEnable()
    {
        if (controls == null)
        {
            controls = new Controls();
            controls.Player.SetCallbacks(this);
        }
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            InteractEvent?.Invoke(true);
        }
        else if (context.canceled)
        {
            InteractEvent?.Invoke(false);
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            DashEvent?.Invoke(true);
        }
        else if (context.canceled)
        {
            DashEvent?.Invoke(false);
        }
    }
}