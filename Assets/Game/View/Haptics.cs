#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;
#endif

namespace Pointless
{
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const int ClockTick = 4;
        const int VirtualKey = 1;
        const int LongPress = 0;

        static readonly jvalue[] tick = { new jvalue { i = ClockTick } };
        static readonly jvalue[] click = { new jvalue { i = VirtualKey } };
        static readonly jvalue[] thump = { new jvalue { i = LongPress } };
        static IntPtr surface, perform;

        public static void Start()
        {
            if (perform != IntPtr.Zero) return;
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var window = activity.Call<AndroidJavaObject>("getWindow");
            using var decor = window.Call<AndroidJavaObject>("getDecorView");
            surface = AndroidJNI.NewGlobalRef(decor.GetRawObject());
            IntPtr type = AndroidJNI.GetObjectClass(surface);
            perform = AndroidJNI.GetMethodID(type, "performHapticFeedback", "(I)Z");
            AndroidJNI.DeleteLocalRef(type);
        }

        public static void Tick() => Play(tick);

        public static void Click() => Play(click);

        public static void Thump() => Play(thump);

        static void Play(jvalue[] effect)
        {
            if (perform != IntPtr.Zero) AndroidJNI.CallBooleanMethod(surface, perform, effect);
        }
#else
        public static void Start() { }

        public static void Tick() { }

        public static void Click() { }

        public static void Thump() { }
#endif
    }
}
