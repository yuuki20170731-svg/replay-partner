using UnityEngine;
using UnityEngine.UIElements;

namespace ReplayPartner
{
    // Articulated UI character: limbs animate independently of puzzle collisions.
    public sealed class DungeonPuppet
    {
        public readonly VisualElement Root = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly VisualElement leftLeg, rightLeg, leftArm, rightArm, torso, cape, lantern;
        private readonly Image paintedCharacter;
        private static Sprite[] paintedFrames;
        public DungeonPuppet(Color cloak)
        {
            Root.style.position = Position.Absolute;
            Root.style.width = Length.Percent(100); Root.style.height = Length.Percent(130);
            Root.style.top = Length.Percent(-30);
            Texture2D sheet = Resources.Load<Texture2D>("explorer-walk-v2");
            if (sheet != null)
            {
                if (paintedFrames == null)
                {
                    paintedFrames = new Sprite[8];
                    int w = sheet.width / 4, h = sheet.height / 2;
                    for (int i = 0; i < 8; i++)
                        paintedFrames[i] = Sprite.Create(sheet, new Rect((i % 4) * w, (1 - i / 4) * h, w, h), new Vector2(.5f, .5f));
                }
                paintedCharacter = new Image { name = "explorer-sprite", sprite = paintedFrames[0], scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                paintedCharacter.style.position = Position.Absolute;
                paintedCharacter.style.left = Length.Percent(-28); paintedCharacter.style.top = Length.Percent(-8);
                paintedCharacter.style.width = Length.Percent(156); paintedCharacter.style.height = Length.Percent(112);
                if (cloak.b > cloak.r * 1.6f) paintedCharacter.tintColor = new Color(.55f, .85f, 1f, .8f);
                Root.Add(paintedCharacter);
                return;
            }
            Color ink = new Color32(22, 25, 27, 255);
            Color leather = new Color32(135, 94, 65, 255);
            Color metal = new Color32(213, 187, 135, 255);
            cloak = Color.Lerp(cloak, new Color32(179, 210, 192, 255), .22f);
            Color darkCloak = Color.Lerp(cloak, ink, .42f);
            // Equipment remains cosmetic; the protagonist is an explorer, not a combat class.
            cape = Part(Root, 18, 31, 64, 54, darkCloak); cape.name = "explorer-cape";
            Edge(cape, ink, 1); cape.style.borderBottomLeftRadius = cape.style.borderBottomRightRadius = 2;
            Part(cape, 12, 22, 8, 66, Color.Lerp(darkCloak, Color.black, .2f));
            Part(cape, 76, 22, 8, 66, Color.Lerp(darkCloak, Color.black, .2f));
            leftLeg = Part(Root, 31, 67, 15, 28, new Color32(56, 52, 45, 255));
            rightLeg = Part(Root, 54, 67, 15, 28, new Color32(56, 52, 45, 255));
            foreach (var leg in new[] { leftLeg, rightLeg })
            {
                Part(leg, -12, 42, 124, 61, leather);
                Part(leg, -12, 82, 135, 18, ink);
                Part(leg, 0, 45, 100, 8, metal);
            }
            leftArm = Part(Root, 14, 39, 19, 34, cloak);
            rightArm = Part(Root, 67, 39, 19, 34, cloak);
            foreach (var arm in new[] { leftArm, rightArm })
            {
                Edge(arm, ink, 1);
                Part(arm, -5, -2, 110, 28, metal);
                Part(arm, 8, 65, 85, 35, leather);
            }
            torso = Part(Root, 29, 33, 42, 43, leather); Edge(torso, ink, 1);
            Part(torso, 6, 6, 88, 22, cloak);
            var strap = Part(torso, 47, 14, 14, 70, metal);
            strap.transform.rotation = Quaternion.Euler(0, 0, 25);
            Part(torso, -5, 74, 110, 13, ink);
            Part(torso, 41, 74, 19, 13, metal);
            Part(torso, -12, 79, 27, 27, leather); // belt pouch
            Part(Root, 33, 35, 34, 9, new Color32(211, 147, 98, 255)); // soft scarf
            Part(Root, 59, 38, 11, 19, new Color32(211, 147, 98, 255));
            var hood = Part(Root, 15, -4, 70, 46, cloak); hood.name = "explorer-hood";
            hood.style.borderTopLeftRadius = hood.style.borderTopRightRadius = hood.style.borderBottomLeftRadius = hood.style.borderBottomRightRadius = 18;
            Edge(hood, metal, 1);
            Part(hood, 12, 22, 76, 68, darkCloak);
            Part(hood, 19, 29, 62, 56, new Color32(250, 216, 179, 255));
            Part(hood, 20, 25, 60, 14, new Color32(100, 69, 49, 255));
            Part(hood, 30, 48, 12, 20, ink); Part(hood, 59, 48, 12, 20, ink);
            Part(hood, 33, 49, 4, 6, Color.white); Part(hood, 62, 49, 4, 6, Color.white);
            Part(hood, 23, 65, 14, 9, new Color32(238, 153, 141, 255));
            Part(hood, 66, 65, 14, 9, new Color32(238, 153, 141, 255));
            Part(hood, 45, 72, 13, 5, new Color32(157, 101, 83, 255));
            lantern = Part(rightArm, 5, 72, 92, 42, ink); lantern.name = "explorer-lantern";
            Edge(lantern, metal, 1);
            Part(lantern, 17, 17, 66, 65, new Color32(255, 190, 74, 255));
            Part(lantern, 38, 24, 24, 49, new Color32(255, 244, 192, 255));
            Part(lantern, 44, 0, 12, 100, metal);
        }
        private static void Edge(VisualElement part, Color color, float width)
        {
            part.style.borderTopColor = part.style.borderBottomColor = part.style.borderLeftColor = part.style.borderRightColor = color;
            part.style.borderTopWidth = part.style.borderBottomWidth = part.style.borderLeftWidth = part.style.borderRightWidth = width;
        }
        private static VisualElement Part(VisualElement parent, float x, float y, float w, float h, Color color)
        {
            var part = new VisualElement { pickingMode = PickingMode.Ignore };
            part.style.position = Position.Absolute; part.style.left = Length.Percent(x); part.style.top = Length.Percent(y);
            part.style.width = Length.Percent(w); part.style.height = Length.Percent(h); part.style.backgroundColor = color;
            part.style.borderTopLeftRadius = part.style.borderTopRightRadius = part.style.borderBottomLeftRadius = part.style.borderBottomRightRadius = 7;
            parent.Add(part); return part;
        }
        public void Animate(float stride, bool walking, float facing, bool hurt, bool backFacing = false)
        {
            float step = walking ? Mathf.Sin(stride) : 0;
            if (paintedCharacter != null)
            {
                int frame = walking ? 1 + Mathf.FloorToInt(Mathf.Repeat(stride, Mathf.PI * 2) / (Mathf.PI * 2) * 3) : 0;
                paintedCharacter.sprite = paintedFrames[(backFacing ? 4 : 0) + frame];
                Root.transform.scale = new Vector3(facing, 1, 1);
                Root.transform.position = new Vector3(0, walking ? -Mathf.Abs(step) : Mathf.Sin(Time.unscaledTime * 2f) * .35f, 0);
                Root.transform.rotation = Quaternion.Euler(0, 0, hurt ? Mathf.Sin(Time.unscaledTime * 40) * 8 : 0);
                Root.style.opacity = hurt ? .55f : 1f;
                return;
            }
            leftLeg.transform.rotation = Quaternion.Euler(0, 0, step * 28);
            rightLeg.transform.rotation = Quaternion.Euler(0, 0, -step * 28);
            leftLeg.transform.position = new Vector3(0, -step * 3, 0);
            rightLeg.transform.position = new Vector3(0, step * 3, 0);
            leftArm.transform.rotation = Quaternion.Euler(0, 0, -step * 22);
            rightArm.transform.rotation = Quaternion.Euler(0, 0, step * 22);
            cape.transform.rotation = Quaternion.Euler(0, 0, -step * 5);
            lantern.transform.rotation = Quaternion.Euler(0, 0, -step * 14);
            torso.transform.position = new Vector3(0, walking ? -Mathf.Abs(step) * 2 : Mathf.Sin(Time.unscaledTime * 2) * .7f, 0);
            Root.transform.scale = new Vector3(facing, 1, 1);
            Root.transform.rotation = Quaternion.Euler(0, 0, hurt ? Mathf.Sin(Time.unscaledTime * 40) * 12 : step * 2);
            Root.style.opacity = hurt ? .5f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 25)) * .5f : 1;
        }
    }
}
