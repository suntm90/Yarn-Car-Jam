using UnityEngine;
using UnityEngine.InputSystem;

public sealed class YarnTest : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current.spaceKey.isPressed)
        {
            Time.timeScale = 0.1f;
        }    
    }
}