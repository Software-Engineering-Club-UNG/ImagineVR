using System.Collections.Generic;
using System.IO;
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
        [Tooltip("Give up on a turn if no data (including server heartbeats) arrives for this long.")]
        public float LineTimeout = 15f;
        [Tooltip("Give up on a turn after this long in total.")]
        public float TotalTimeout = 90f;

        /// <summary>
        /// A file on the device that overrides the built-in address and token, so the real token never
        /// has to be committed or baked into the APK, and the server can change without a rebuild:
        ///   {"server_url": "https://1-2-3-4.sslip.io", "app_token": "..."}
        /// On the Quest it is /sdcard/Android/data/com.hacklanta.imaginevr/files/imagine.json.
        /// </summary>
        public static string OverridePath => Path.Combine(Application.persistentDataPath, "imagine.json");

        /// <summary>Where the address came from, for the terminal: "built in" or "imagine.json".</summary>
        public string Source = "built in";

        public static ImagineSettings Load()
        {
            ImagineSettings built = Resources.Load<ImagineSettings>("ImagineSettings");
            // a copy, so that an override on this machine is never written back into the asset
            ImagineSettings s = built != null ? Instantiate(built) : CreateInstance<ImagineSettings>();
            try
            {
                if (File.Exists(OverridePath) && MiniJson.Parse(File.ReadAllText(OverridePath)) is Dictionary<string, object> file)
                {
                    if (file.TryGetValue("server_url", out object url) && url is string u && u.Trim().Length > 0) s.ServerUrl = u.Trim().TrimEnd('/');
                    if (file.TryGetValue("app_token", out object token) && token is string t && t.Trim().Length > 0) s.AppToken = t.Trim();
                    s.Source = "imagine.json";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("ImagineVR: could not read " + OverridePath + ": " + e.Message);
            }
            return s;
        }
    }
}
