using System;
using UnityEngine;

namespace DoHwa.Planet
{
    [CreateAssetMenu(menuName = "Mote/Planet Decoration Catalog")]
    public sealed class PlanetDecorationCatalog : ScriptableObject
    {
        [Serializable]
        public struct Decoration
        {
            public string kind;
            public string displayName;
            public GameObject model;
            public GameObject gift;
        }

        [Serializable]
        public struct SkyPreset
        {
            public string displayName;
            public Material material;
            public Material cloudMaterial;
        }

        [SerializeField] private SkyPreset[] skies;
        public SkyPreset Sky(int index) => skies[index >= 0 && index < skies.Length ? index : 0];

        [SerializeField] private Decoration[] entries;
        public Decoration this[int index] => entries[index];

        public void Draw(PlanetItem item, PlanetView surface, Transform parent)
        {
            int index = Array.FindIndex(entries, entry => entry.kind == item.kind);
            if (index < 0) return;
            Decoration decoration = entries[index];
            Transform model = Instantiate(decoration.model, parent, false).transform;
            Vector3 direction = item.Direction.normalized;
            model.localPosition = surface.SurfacePosition(direction);
            model.localRotation = Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.AngleAxis(item.yaw, Vector3.up);
            if (decoration.gift && !string.IsNullOrEmpty(item.giftFrom))
                Instantiate(decoration.gift, model, false);
        }
    }
}
