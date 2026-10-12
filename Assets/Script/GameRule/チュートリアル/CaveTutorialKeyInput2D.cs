using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Player側の入力設定が旧Input / 新Input / Bothでも、移動チュートリアルを動かすための小さな補助。
public static class CaveTutorialKeyInput2D
{
    public enum Key { A, D, Space }

    public static bool WasPressed(Key key)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            switch (key)
            {
                case Key.A: return keyboard.aKey.wasPressedThisFrame;
                case Key.D: return keyboard.dKey.wasPressedThisFrame;
                case Key.Space: return keyboard.spaceKey.wasPressedThisFrame;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        switch (key)
        {
            case Key.A: return Input.GetKeyDown(KeyCode.A);
            case Key.D: return Input.GetKeyDown(KeyCode.D);
            case Key.Space: return Input.GetKeyDown(KeyCode.Space);
        }
#endif
        return false;
    }
}
