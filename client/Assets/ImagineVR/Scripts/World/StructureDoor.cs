using UnityEngine;

namespace ImagineVR
{
    /// <summary>A controlled hinge, not a loose grabbable prop. Blocks passage while closed.</summary>
    public sealed class StructureDoor : MonoBehaviour
    {
        public string Id { get; private set; }
        public bool Open => angle > 45;
        public bool WantsOpen { get; private set; }
        float angle, highlightedAt = -10;
        BoxCollider box;
        Mesh mesh;
        MeshRenderer visual;
        MaterialPropertyBlock properties;
        readonly Collider[] blockers = new Collider[32];
        public void Init(string id, Material material, Color color)
        {
            Id = id;
            baseYaw = transform.localEulerAngles.y;
            properties = new MaterialPropertyBlock();
            var mb = new MeshBuilder(); mb.Col(color).Box(new Vector3(0.6f, 1.08f, 0), new Vector3(1.16f, 2.16f, 0.12f));
            mb.Col("#B49B63").Box(new Vector3(1.05f, 1, -0.09f), new Vector3(0.08f, 0.08f, 0.1f));
            mesh = mb.Build("Door"); gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual = gameObject.AddComponent<MeshRenderer>(); visual.sharedMaterial = material;
            box = gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0.6f, 1.08f, 0); box.size = new Vector3(1.16f, 2.16f, 0.12f);
        }
        public void Set(string state)
        {
            WantsOpen = state == "toggle" ? !WantsOpen : state == "open";
            Sfx.Play("tap", transform.position, 0.35f);
            SessionLog.Write("door", "id", Id, "requested", WantsOpen ? "open" : "closed");
        }
        public void Highlight() { highlightedAt = Time.time; }
        public bool Advance(float dt)
        {
            float next = Mathf.MoveTowards(angle, WantsOpen ? 90 : 0, dt * 120);
            Quaternion baseRotation = transform.parent.rotation * Quaternion.Euler(0, baseYaw, 0);
            Quaternion q = baseRotation * Quaternion.Euler(0, -next, 0);
            Vector3 centre = transform.position + q * box.center;
            Physics.SyncTransforms();
            int n = Physics.OverlapBoxNonAlloc(centre, box.size / 2 + Vector3.one * 0.04f, blockers, q, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = n == blockers.Length;
            for (int i = 0; i < n; i++)
                if (blockers[i] != box && (blockers[i] is CharacterController || blockers[i].GetComponentInParent<Rig>() != null || blockers[i].attachedRigidbody != null)) blocked = true;
            if (!blocked) { angle = next; transform.localRotation = Quaternion.Euler(0, baseYaw - angle, 0); }
            return !blocked;
        }
        float baseYaw;
        void Update()
        {
            Advance(Time.deltaTime);
            properties.SetColor("_Tint", Time.time - highlightedAt < 0.1f ? new Color(1.25f, 1.15f, 0.75f) : Color.white);
            properties.SetFloat("_Appear", 1); properties.SetFloat("_Sway", 0); visual.SetPropertyBlock(properties);
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
