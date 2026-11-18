using UnityEngine;
using UnityEngine.EventSystems;

namespace GuiQin
{
    // 鼠标 / 触屏统一成一个“指针”。新旧输入系统都能编（由 Player Settings 的 Active Input Handling 决定走哪个分支）。
    public static class PointerInput
    {
        public static bool Down, Held, Up;
        public static Vector2 Pos;

        public static void Poll()
        {
#if ENABLE_INPUT_SYSTEM
            var p = UnityEngine.InputSystem.Pointer.current;
            if (p == null) { Down = Held = Up = false; return; }
            Down = p.press.wasPressedThisFrame; Held = p.press.isPressed; Up = p.press.wasReleasedThisFrame;
            Pos = p.position.ReadValue();
#else
            Down = Input.GetMouseButtonDown(0); Held = Input.GetMouseButton(0); Up = Input.GetMouseButtonUp(0);
            Pos = Input.mousePosition;
#endif
        }

        public static bool OverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
