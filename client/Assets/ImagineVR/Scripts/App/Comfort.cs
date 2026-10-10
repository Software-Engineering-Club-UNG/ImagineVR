using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// Narrows the user's view while they are being moved, and opens it again when they stop.
    /// Motion at the edge of vision is what unsettles the stomach; the middle is left alone.
    /// </summary>
    public sealed class Comfort : MonoBehaviour
    {
        static readonly int AmountId = Shader.PropertyToID("_Amount"), InnerId = Shader.PropertyToID("_Inner");
        Material _material;
        MeshRenderer _renderer;
        float _level;

        public static Comfort Create(Transform head)
        {
            var go = new GameObject("Comfort");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            var mesh = new Mesh { name = "Comfort" };
            mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Comfort c = go.AddComponent<Comfort>();
            c._material = new Material(Shader.Find("ImagineVR/Vignette")) { name = "Comfort" };
            c._renderer = go.AddComponent<MeshRenderer>();
            c._renderer.sharedMaterial = c._material;
            c._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c._renderer.receiveShadows = false;
            c._renderer.enabled = false;
            return c;
        }

        /// <summary>How much to narrow the view, 0 to 1. It closes quickly and opens gently.</summary>
        public void Set(float target, float dt)
        {
            target = Mathf.Clamp01(target);
            _level = Mathf.MoveTowards(_level, target, dt * (target > _level ? 3f : 1.5f));
            bool show = _level > 0.01f;
            if (_renderer.enabled != show) _renderer.enabled = show;
            if (!show) return;
            _material.SetFloat(AmountId, Mathf.Clamp01(_level * 2.5f));
            _material.SetFloat(InnerId, Mathf.Lerp(1.15f, 0.5f, _level));
        }
    }
}
