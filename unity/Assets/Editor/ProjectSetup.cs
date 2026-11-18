using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuiQin.EditorTools
{
    // 第一次打开工程时设一次竖屏等基础参数；也可以从菜单「归秦」手动再跑一遍。
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string DoneKey = "GuiQin.ProjectSetup.v1";

        static ProjectSetup()
        {
            if (EditorPrefs.GetBool(DoneKey, false)) return;
            EditorApplication.delayCall += () => { ApplyPlayerSettings(); EditorPrefs.SetBool(DoneKey, true); };
        }

        [MenuItem("归秦/应用竖屏等项目设置")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "归秦";
            PlayerSettings.companyName = "GuiQin";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 720;
            PlayerSettings.defaultScreenHeight = 1280;
            PlayerSettings.runInBackground = true;
            Debug.Log("[归秦] 已应用：竖屏 720×1280。");
        }

        [MenuItem("归秦/创建 Main 场景并加入 Build")]
        public static void CreateMainScene()
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            Debug.Log("[归秦] 已创建 Assets/Scenes/Main.unity。游戏对象由 Bootstrap 在运行时自动创建，空场景即可。");
        }
    }
}
