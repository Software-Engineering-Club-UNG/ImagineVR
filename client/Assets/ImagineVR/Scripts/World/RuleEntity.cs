using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    // Identity exists even when a placement is GPU-instanced and has no GameObject.
    public sealed class RuleEntity
    {
        public string Id, GroupId, Asset;
        public readonly HashSet<string> Tags = new HashSet<string>();
        public Group Group;
        public int Index = -1;
        public GameObject Object;
        public Placement Original;
        public bool Removed, Visible = true;
        public float Health = -1, Durability = -1, Gravity = 1;
        public Vector3 BirthPosition;
        public Quaternion BirthRotation;
        public Vector3 BirthScale;
        public World World;
        public bool Alive => !Removed && (Index >= 0 || Object != null);
        public Vector3 Position
        {
            get => Object != null ? Object.transform.position : Original.Position + (Original.Grounded && World != null ? Vector3.up * World.Terrain.HeightAt(Original.Position.x, Original.Position.z) : Vector3.zero);
            set { if (Object != null) Object.transform.position = value; else { Original.Position = value; Original.Grounded = false; UpdatePlacement(); } }
        }
        public void UpdatePlacement()
        {
            if (Group == null || Index < 0 || Index >= Group.Items.Count) return;
            Placement p = Original;
            if (Removed || !Visible || Object != null) p.Mesh = null;
            Group.Items[Index] = p;
            Group.Dirty = true;
        }
        public void Tint(Color color)
        {
            Original.Tint = color;
            Original.AbsoluteTint = true;
            if (Object != null)
            {
                Prop prop = Object.GetComponent<Prop>();
                if (prop != null) prop.SetTint(color);
                foreach (Renderer renderer in Object.GetComponentsInChildren<Renderer>())
                {
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    block.SetColor("_Tint", color); block.SetFloat("_Appear", 1f); renderer.SetPropertyBlock(block);
                }
            }
            UpdatePlacement();
        }
        public void Resize(float scale)
        {
            if (Object != null) Object.transform.localScale = BirthScale * scale;
            else Original.Scale = BirthScale * scale;
            UpdatePlacement();
        }
        public void Show(bool visible)
        {
            Visible = visible;
            if (Object != null) foreach (Renderer r in Object.GetComponentsInChildren<Renderer>()) r.enabled = visible;
            UpdatePlacement();
        }
    }
}
