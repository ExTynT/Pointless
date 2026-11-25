using TMPro;
using UnityEngine;

namespace Pointless
{
    public sealed class Labels
    {
        const string FontName = "VarelaRound-Regular";

        static TMP_FontAsset font;
        readonly TextMeshPro[] pool;
        readonly bool[] shown;
        readonly float capScale;
        int used;

        public Labels(Transform parent, int count, int order)
        {
            if (font == null) font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>(FontName));
            pool = new TextMeshPro[count];
            shown = new bool[count];
            for (int i = 0; i < count; i++)
            {
                var label = new GameObject("Label").AddComponent<TextMeshPro>();
                label.transform.SetParent(parent, false);
                label.font = font;
                label.isOrthographic = true;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.horizontalAlignment = HorizontalAlignmentOptions.Center;
                label.verticalAlignment = VerticalAlignmentOptions.Capline;
                label.rectTransform.sizeDelta = Vector2.zero;
                label.renderer.sortingOrder = order;
                pool[i] = label;
            }

            TextMeshPro probe = pool[0];
            probe.fontSize = 10f;
            probe.text = "H";
            probe.ForceMeshUpdate(true, true);
            capScale = probe.textBounds.size.y / 10f;
            foreach (TextMeshPro label in pool) label.gameObject.SetActive(false);
        }

        public void Begin() => used = 0;

        public void Put(string text, float x, float y, float cap, Color32 tint, bool left = false)
        {
            if (used == pool.Length || tint.a == 0) return;
            int index = used++;
            TextMeshPro label = pool[index];
            if (!shown[index])
            {
                label.gameObject.SetActive(true);
                shown[index] = true;
            }
            label.text = text;
            label.horizontalAlignment = left ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Center;
            label.fontSize = cap / capScale;
            label.color = tint;
            label.transform.localPosition = new Vector3(x, y, 0f);
        }

        public void End()
        {
            for (int i = used; i < pool.Length; i++)
            {
                if (!shown[i]) continue;
                pool[i].gameObject.SetActive(false);
                shown[i] = false;
            }
        }

        public void Dispose()
        {
            foreach (TextMeshPro label in pool)
                if (label != null) Object.Destroy(label.gameObject);
        }
    }
}
