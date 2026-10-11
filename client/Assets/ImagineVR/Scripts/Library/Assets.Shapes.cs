using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public static partial class Library
    {
        /// <summary>
        /// Plain shapes. Each is about 1 m, white (so a tint gives its exact colour), and has its
        /// origin at the centre of its base. They are the last-resort stand-ins and the building
        /// blocks for sculpting things that are not in the library.
        /// </summary>
        static partial void RegisterShapes()
        {
            AssetDef Shape(string id, string desc, System.Action<MeshBuilder> recipe, ColliderShape collider = ColliderShape.Box)
            {
                AssetDef def = Add(id, "shapes", 1f, desc, "shape,primitive,block", "", mb => { mb.Col(Color.white); recipe(mb); });
                def.Primitive = true;
                def.Solid(4f, 0.25f, collider);
                return def;
            }

            Shape("cube", "Plain cube", mb => mb.BoxOn(Vector3.zero, Vector3.one));
            Shape("sphere", "Plain sphere", mb => mb.Sphere(new Vector3(0f, 0.5f, 0f), 0.5f, 2), ColliderShape.Sphere).Bounce = 0.6f;
            Shape("cylinder", "Plain cylinder", mb => mb.Cyl(Vector3.zero, 0.5f, 0.5f, 1f, 16));
            Shape("cone", "Plain cone", mb => mb.Cone(Vector3.zero, 0.5f, 1f, 16));
            Shape("capsule", "Plain capsule, half as wide as it is tall", mb => mb.Capsule(Vector3.zero, 0.25f, 1f, 12), ColliderShape.Capsule);
            Shape("pyramid", "Plain four-sided pyramid", mb => mb.Pyramid(Vector3.zero, 1f, 1f));
            Shape("torus", "Plain ring lying flat", mb => mb.Torus(new Vector3(0f, 0.12f, 0f), 0.38f, 0.12f, 18, 8));
            Shape("disc", "Plain thin disc lying flat", mb => mb.Cyl(Vector3.zero, 0.5f, 0.5f, 0.06f, 20));
            Shape("wedge", "Plain ramp, high at the far end", mb => mb.Wedge(Vector3.zero, Vector3.one));
            Shape("blob", "Plain lumpy ball: a rock, a slime, a bush, a cloud", mb => mb.Blob(new Vector3(0f, 0.5f, 0f), 0.5f, 0.18f, 3, 2), ColliderShape.Sphere);
            Shape("dome", "Plain half ball standing on its flat side", mb => mb.Dome(Vector3.zero, 0.5f, 0.5f, 14, 5));
            Shape("plank", "Plain long flat board, 2 m by 0.3 m", mb => mb.BoxOn(Vector3.zero, new Vector3(0.3f, 0.06f, 2f)));
        }
    }
}
