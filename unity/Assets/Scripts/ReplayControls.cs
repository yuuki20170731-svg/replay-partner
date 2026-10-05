using System;
using UnityEngine;

namespace ReplayPartner
{
    public static class ReplayControls
    {
        public static KeyCode[] Defaults => new[] { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.E, KeyCode.R, KeyCode.Backspace };

        public static bool CanAssign(KeyCode[] keys, int index, KeyCode key)
        {
            if (keys == null || keys.Length != 7 || index < 0 || index >= keys.Length || !Enum.IsDefined(typeof(KeyCode), key)) return false;
            if (key == KeyCode.None || key >= KeyCode.Mouse0 || key == KeyCode.Escape || key == KeyCode.Return ||
                key == KeyCode.KeypadEnter || key == KeyCode.Space || key == KeyCode.Tab || key == KeyCode.F1 ||
                key == KeyCode.UpArrow || key == KeyCode.DownArrow || key == KeyCode.LeftArrow || key == KeyCode.RightArrow) return false;
            for (int i = 0; i < keys.Length; i++) if (i != index && keys[i] == key) return false;
            return true;
        }

        public static bool Valid(KeyCode[] keys)
        {
            if (keys == null || keys.Length != 7) return false;
            for (int i = 0; i < keys.Length; i++) if (!CanAssign(keys, i, keys[i])) return false;
            return true;
        }
    }
}
