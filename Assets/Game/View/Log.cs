using System.Diagnostics;
using UnityEngine;

namespace Pointless
{
    public static class Log
    {
        const string Tag = "Pointless";

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
#if UNITY_6000_6_OR_NEWER
        [Conditional("UNITY_INCLUDE_INSTRUMENTATION")]
#else
        [Conditional("DEVELOPMENT_BUILD")]
#endif
        public static void Info(string message) => UnityEngine.Debug.unityLogger.Log(Tag, message);

        [HideInCallstack]
        public static void Warn(string message) => UnityEngine.Debug.unityLogger.LogWarning(Tag, message);
    }
}
