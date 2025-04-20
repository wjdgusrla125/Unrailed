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
    public event Action<bool> OneRailEvent;
    
    private Controls controls;

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

    public void OnOneRail(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OneRailEvent?.Invoke(true);
        }
        else if (context.canceled)
        {
            OneRailEvent?.Invoke(false);
        }
    }
}