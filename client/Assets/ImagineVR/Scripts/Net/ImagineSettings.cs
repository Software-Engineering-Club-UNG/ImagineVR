using UnityEngine;

namespace ImagineVR
{
    /// <summary>Where the app finds its server. Lives in Resources so it is the same in the editor and on the headset.</summary>
    [CreateAssetMenu(menuName = "ImagineVR/Settings", fileName = "ImagineSettings")]
    public sealed class ImagineSettings : ScriptableObject
    {
        [Tooltip("Base URL of the ImagineVR server, e.g. https://1-2-3-4.sslip.io")]
        public string ServerUrl = "http://127.0.0.1:8080";
        [Tooltip("Must match IMAGINE_APP_TOKEN on the server.")]
        public string AppToken = "dev-token";
        [Tooltip("Give up on a turn if no new line arrives for this long.")]
        public float LineTimeout = 8f;
        [Tooltip("Give up on a turn after this long in total.")]
        public float TotalTimeout = 45f;

        public static ImagineSettings Load()
        {
            ImagineSettings s = Resources.Load<ImagineSettings>("ImagineSettings");
            return s != null ? s : CreateInstance<ImagineSettings>();
        }
    }
}
