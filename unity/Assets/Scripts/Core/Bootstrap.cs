using UnityEngine;

namespace GuiQin
{
    // 不依赖场景：任何场景一按 Play，都会自动创建游戏对象
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run()
        {
            if (Object.FindFirstObjectByType<GameController>() != null) return;
            new GameObject("GuiQin").AddComponent<GameController>();
        }
    }
}
