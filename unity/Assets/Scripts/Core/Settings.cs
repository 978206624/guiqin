using System;
using UnityEngine;

namespace GuiQin
{
    // 设备上的偏好设置（画面特效开关、战斗速度）。只是“这台手机怎么显示”，不是游戏进度，所以存在 PlayerPrefs，不走后端。
    public static class Settings
    {
        public static event Action Changed;
        static bool Get(string k) { return PlayerPrefs.GetInt("gq_" + k, 1) == 1; }
        static void Set(string k, bool v) { PlayerPrefs.SetInt("gq_" + k, v ? 1 : 0); PlayerPrefs.Save(); if (Changed != null) Changed(); }

        public static bool Water { get { return Get("water"); } set { Set("water", value); } }          // 水面动效
        public static bool Butterflies { get { return Get("bf"); } set { Set("bf", value); } }          // 蝴蝶
        public static bool WeatherFx { get { return Get("wx"); } set { Set("wx", value); } }            // 雨雪雷的画面
        public static bool Shake { get { return Get("shake"); } set { Set("shake", value); } }          // 战斗震屏
        public static float Speed
        {
            get { float v = PlayerPrefs.GetFloat("gq_speed", 1f); return v < 1f ? 1f : v > 2f ? 2f : v; }
            set { PlayerPrefs.SetFloat("gq_speed", value); PlayerPrefs.Save(); if (Changed != null) Changed(); }
        }
    }
}
