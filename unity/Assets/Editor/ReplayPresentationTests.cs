using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReplayPartner.Editor
{
    public static class ReplayPresentationTests
    {
        [MenuItem("Replay Partner/Run Presentation Tests")]
        public static void Run()
        {
            var host = new GameObject("Presentation test");
            try
            {
                var app = host.AddComponent<ReplayApp>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ReplayApp).GetMethod("Awake", flags).Invoke(app, null);
                var screen = typeof(ReplayApp).GetNestedType("Screen", BindingFlags.NonPublic);
                var show = typeof(ReplayApp).GetMethod("Show", flags);
                var load = typeof(ReplayApp).GetMethod("LoadStage", flags);
                var field = typeof(ReplayApp).GetField("simulation", flags);
                for (int i = 0; i < 10; i++)
                {
                    load.Invoke(app, new object[] { i });
                    var sim = (ReplaySimulation)field.GetValue(app);
                    sim.Begin(); sim.Advance(Vector2.right); sim.Commit();
                    sim.Begin(); sim.Advance(Vector2.down); sim.Commit();
                    show.Invoke(app, new[] { Enum.Parse(screen, "Playing") });
                    sim.Begin(); // A third recording is refused without changing the room.
                    show.Invoke(app, new[] { Enum.Parse(screen, "Clear") });
                    show.Invoke(app, new[] { Enum.Parse(screen, "Failed") });
                }
                foreach (string name in new[] { "Title", "Select", "Pause", "Help", "Settings", "Complete", "Credits" })
                    show.Invoke(app, new[] { Enum.Parse(screen, name) });
                var root = host.GetComponent<UIDocument>().rootVisualElement;
                show.Invoke(app, new[] { Enum.Parse(screen, "Playing") });
                var savedSimulation = field.GetValue(app);
                typeof(ReplayApp).GetMethod("RequestQuit", flags).Invoke(app, null);
                if (typeof(ReplayApp).GetField("screen", flags).GetValue(app).ToString() != "QuitConfirm") throw new Exception("Quit dialog not shown");
                typeof(ReplayApp).GetMethod("CancelQuit", flags).Invoke(app, null);
                if (typeof(ReplayApp).GetField("screen", flags).GetValue(app).ToString() != "Playing" || !ReferenceEquals(savedSimulation, field.GetValue(app))) throw new Exception("Cancel quit must preserve room");
                if (root == null || root.childCount == 0) throw new Exception("Presentation root is empty");
                Debug.Log("Replay Partner presentation tests passed: all ten rooms with two clones/trails, result/failure and six menu screens. Not a manual playthrough or pixel-layout test.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
