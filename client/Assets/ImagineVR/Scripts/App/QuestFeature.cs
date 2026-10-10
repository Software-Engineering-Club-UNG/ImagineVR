using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace ImagineVR
{
    /// <summary>
    /// The two things a Quest can do that Unity's OpenXR plugin has no switch for:
    ///   - run the display at 90 Hz rather than the 72 it starts at;
    ///   - show the real room behind the app (passthrough), so the dark room the user starts in
    ///     is their own room, and the first world replaces it.
    /// Both are asked for politely: on a headset that offers neither, nothing changes.
    /// </summary>
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "ImagineVR Quest extras",
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "ImagineVR",
        Desc = "90 Hz display and a passthrough opening on Meta Quest.",
        OpenxrExtensionStrings = Extensions,
        Version = "1.0.0",
        FeatureId = Id)]
#endif
    public sealed class QuestFeature : OpenXRFeature
    {
        public const string Id = "com.imaginevr.quest";
        public const string Extensions = "XR_FB_display_refresh_rate";

        [Tooltip("Asked of the headset when a session begins. 0 leaves it alone.")]
        public float refreshRate = 90f;
        [Tooltip("Show the real room in place of the dark room.")]
        public bool passthrough = true;

        /// <summary>True once the headset has agreed to blend the app over the real room.</summary>
        public static bool Passthrough { get; private set; }

        const int PrimaryStereo = 2;   // XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int GetProcAddr(ulong instance, [MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr function);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int RequestRefreshRate(ulong session, float rate);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int EnumerateBlendModes(ulong instance, ulong system, int viewConfiguration, uint capacity, out uint count, IntPtr modes);

        ulong _instance, _system;

        protected override bool OnInstanceCreate(ulong xrInstance)
        {
            _instance = xrInstance;
            Passthrough = false;
            return true;
        }

        protected override void OnSystemChange(ulong xrSystem)
        {
            _system = xrSystem;
        }

        protected override void OnSessionBegin(ulong xrSession)
        {
            try
            {
                if (refreshRate > 0f) AskRefreshRate(xrSession);
                if (passthrough) AskPassthrough();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ImagineVR] Quest extras skipped: " + e.Message);
            }
        }

        protected override void OnSessionEnd(ulong xrSession)
        {
            Passthrough = false;
        }

        T Find<T>(string name) where T : Delegate
        {
            IntPtr lookup = xrGetInstanceProcAddr;
            if (lookup == IntPtr.Zero || _instance == 0) return null;
            var get = Marshal.GetDelegateForFunctionPointer<GetProcAddr>(lookup);
            return get(_instance, name, out IntPtr function) >= 0 && function != IntPtr.Zero
                ? Marshal.GetDelegateForFunctionPointer<T>(function) : null;
        }

        void AskRefreshRate(ulong session)
        {
            var request = Find<RequestRefreshRate>("xrRequestDisplayRefreshRateFB");
            if (request == null) { Debug.Log("[ImagineVR] refresh rate: the headset does not offer a choice"); return; }
            int result = request(session, refreshRate);
            Debug.Log("[ImagineVR] refresh rate: asked for " + refreshRate + " Hz, result " + result);
        }

        void AskPassthrough()
        {
            // Only ask for what the headset lists, since asking for a blend it lacks would stop every frame.
            var enumerate = Find<EnumerateBlendModes>("xrEnumerateEnvironmentBlendModes");
            if (enumerate == null || _system == 0) return;
            IntPtr modes = Marshal.AllocHGlobal(sizeof(int) * 8);
            try
            {
                bool offered = false;
                if (enumerate(_instance, _system, PrimaryStereo, 8, out uint count, modes) >= 0)
                    for (int i = 0; i < count && i < 8; i++) offered |= Marshal.ReadInt32(modes, i * sizeof(int)) == (int)XrEnvironmentBlendMode.AlphaBlend;
                if (offered) SetEnvironmentBlendMode(XrEnvironmentBlendMode.AlphaBlend);
                Passthrough = offered;
                Debug.Log("[ImagineVR] passthrough: " + (offered ? "on" : "not offered by this headset") + " (" + count + " blend modes)");
            }
            finally
            {
                Marshal.FreeHGlobal(modes);
            }
        }
    }
}
