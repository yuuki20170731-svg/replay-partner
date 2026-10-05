using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReplayPartner
{
    public sealed class ReplayApp : MonoBehaviour
    {
        private enum Screen { Title, Intro, Playing, Pause, Select, Help, Settings, Clear, Complete, Failed, Credits }
        private static readonly Color Ink = new Color32(238, 231, 208, 255);
        private static readonly Color Muted = new Color32(163, 169, 159, 255);
        private static readonly Color Night = new Color32(9, 18, 23, 255);
        private static readonly Color Surface = new Color32(19, 34, 40, 245);
        private static readonly Color Tile = new Color32(40, 55, 59, 255);
        private static readonly Color Line = new Color32(85, 98, 93, 255);
        private static readonly Color Mint = new Color32(126, 209, 189, 255);
        private static readonly Color Gold = new Color32(247, 196, 108, 255);
        private static readonly Color Blue = new Color32(132, 199, 225, 255);

        private UIDocument document;
        private VisualElement root;
        private VisualElement grid;
        private VisualElement timeline;
        private Label statusText;
        private Label guideText;
        private Label feedbackText;
        private Label partnerText;
        private Button recordButton;
        private Button undoButton;
        private Screen screen = Screen.Title;
        private Screen previous = Screen.Title;
        private ReplaySimulation simulation;
        private int stageIndex;
        private int unlocked;
        private int clearedMask;
        private int hintLevel;
        private float accumulator;
        private float nextUiRefresh;
        private VisualElement[,] boardContents;
        private VisualElement[,] boardVeils;
        private VisualElement actorLayer;
        private VisualElement routeLayer;
        private VisualElement flashLayer;
        private bool showRoutes = true;
        private bool reducedMotion;
        private readonly KeyCode[] controls = ReplayControls.Defaults;
        private int screenChangedFrame = -1;
        private readonly string[] controlNames = { "上", "下", "左", "右", "記録・確定", "リトライ", "記録取消・分身削除" };
        private int pendingBinding = -1;
        private string bindingMessage = "変更したい操作を選び、キーを押してください。";
        private float hurtUntil;
        private float flashUntil;
        private float attemptSeconds;
        private int attemptRetries;
        private float gateAChanged = -100;
        private float gateBChanged = -100;
        private readonly List<VisualElement> actorVisuals = new List<VisualElement>();
        private sealed class ActorMotion
        {
            public Vector2 Previous;
            public float Stride;
            public float Facing = 1;
            public bool BackFacing;
            public float LastMoved = -1;
            public DungeonPuppet Puppet;
            public VisualElement Shadow;
        }
        private float volume;
        private AudioSource audioSource;
        private Texture2D floorArt;
        private Texture2D wallArt;
        private Texture2D gateArt;
        private Texture2D exitArt;
        private Texture2D keyArt;
        private Texture2D crateArt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (FindFirstObjectByType<ReplayApp>() == null)
                new GameObject("Replay Partner").AddComponent<ReplayApp>();
        }

        private void Awake()
        {
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            unlocked = Mathf.Clamp(PlayerPrefs.GetInt("rp.unlocked", 1), 1, ReplayStage.All.Count);
            clearedMask = PlayerPrefs.GetInt("rp.cleared", 0) & 1023;
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat("rp.volume", 0.35f));
            reducedMotion = PlayerPrefs.GetInt("rp.reducedMotion", 0) == 1;
            for (int i = 0; i < controls.Length; i++)
            {
                int saved = PlayerPrefs.GetInt("rp.key." + i, (int)controls[i]);
                if (Enum.IsDefined(typeof(KeyCode), saved)) controls[i] = (KeyCode)saved;
            }
            if (!ReplayControls.Valid(controls))
            {
                Array.Copy(ReplayControls.Defaults, controls, controls.Length);
                bindingMessage = "保存キーに重複・使用不可キーがあったため、初期キーで起動しました。";
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            document = gameObject.AddComponent<UIDocument>();
            PanelSettings panel = Resources.Load<PanelSettings>("ReplayPanel");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 720);
            }
            document.panelSettings = panel;
            root = document.rootVisualElement;
            Font font = Resources.Load<Font>("NotoSansCJKjp-Regular");
            if (font != null) root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            floorArt = Resources.Load<Texture2D>("DungeonFloor");
            wallArt = Resources.Load<Texture2D>("DungeonWall");
            gateArt = Resources.Load<Texture2D>("DungeonGate");
            exitArt = Resources.Load<Texture2D>("DungeonExit");
            keyArt = Resources.Load<Texture2D>("DungeonKey");
            crateArt = Resources.Load<Texture2D>("DungeonCrate");
            Show(Screen.Title);
        }

        private void Update()
        {
            if (screenChangedFrame == Time.frameCount) return;
            if (screen == Screen.Settings && pendingBinding >= 0 && Input.anyKeyDown)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { pendingBinding = -1; Show(Screen.Settings); return; }
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    if ((int)key >= (int)KeyCode.Mouse0 || !Input.GetKeyDown(key)) continue;
                    if (!ReplayControls.CanAssign(controls, pendingBinding, key)) { bindingMessage = "そのキーは予約済み・使用中です。別のキーを押してください。"; Show(Screen.Settings); return; }
                    controls[pendingBinding] = key; PlayerPrefs.SetInt("rp.key." + pendingBinding, (int)key); PlayerPrefs.Save();
                    pendingBinding = -1; bindingMessage = "キー設定を保存しました。"; Show(Screen.Settings); return;
                }
            }
            if (screen == Screen.Title && Input.GetKeyDown(KeyCode.Return)) { LoadStage(0); return; }
            if (screen == Screen.Intro && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))) { Show(Screen.Playing); return; }
            if (screen == Screen.Pause && Input.GetKeyDown(KeyCode.Escape)) { Show(Screen.Playing); return; }
            if ((screen == Screen.Help || screen == Screen.Settings || screen == Screen.Credits) && Input.GetKeyDown(KeyCode.Escape)) { pendingBinding = -1; Show(previous); return; }
            if (screen == Screen.Clear && Input.GetKeyDown(KeyCode.Return)) { if (stageIndex == 9) Show(Screen.Complete); else LoadStage(stageIndex + 1); return; }
            if (screen == Screen.Complete && Input.GetKeyDown(KeyCode.Return)) { Show(Screen.Select); return; }
            if (screen == Screen.Failed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(controls[5]))) { Retry(); return; }
            if (screen != Screen.Playing) return;
            if (Input.GetKeyDown(KeyCode.F1)) { previous = screen; Show(Screen.Help); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { Show(Screen.Pause); return; }
            if (Input.GetKeyDown(KeyCode.Tab)) { showRoutes = !showRoutes; if (routeLayer != null) routeLayer.style.display = showRoutes ? DisplayStyle.Flex : DisplayStyle.None; }
            if (stageIndex > 0 && Input.GetKeyDown(controls[4])) ToggleRecord();
            if (Input.GetKeyDown(controls[5])) Retry();
            if (Input.GetKeyDown(controls[6])) Undo();
            accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Vector2 direction = ReadDirection();
            int oldHealth = simulation.Health;
            bool oldKey = simulation.HasKey, oldA = simulation.OpenA, oldB = simulation.OpenB;
            while (accumulator >= ReplaySimulation.StepSeconds)
            {
                accumulator -= ReplaySimulation.StepSeconds;
                simulation.Advance(direction);
                attemptSeconds += ReplaySimulation.StepSeconds;
                if (simulation.Mode == ReplayMode.Cleared) { ClearStage(); return; }
                if (simulation.Mode == ReplayMode.Failed) { Sound(160); Show(Screen.Failed); return; }
            }
            if (simulation.Health < oldHealth) { hurtUntil = Time.unscaledTime + .65f; Flash(new Color(1, .15f, .1f, .18f), 160); }
            else if (!oldKey && simulation.HasKey) Flash(new Color(1, .8f, .25f, .17f), 920);
            else if (oldA != simulation.OpenA || oldB != simulation.OpenB) Flash(new Color(.3f, .9f, .75f, .1f), (!oldA && simulation.OpenA) || (!oldB && simulation.OpenB) ? 620 : 280);
            if (oldA != simulation.OpenA) gateAChanged = Time.unscaledTime;
            if (oldB != simulation.OpenB) gateBChanged = Time.unscaledTime;
            if (flashLayer != null) flashLayer.style.opacity = reducedMotion ? 0 : Mathf.Clamp01((flashUntil - Time.unscaledTime) / .4f);
            DrawActors();
            if (Time.unscaledTime >= nextUiRefresh) { nextUiRefresh = Time.unscaledTime + 0.1f; RefreshGame(); }
        }

        private Vector2 ReadDirection()
        {
            float x = 0, y = 0;
            if (Input.GetKey(controls[0]) || Input.GetKey(KeyCode.UpArrow)) y--;
            if (Input.GetKey(controls[1]) || Input.GetKey(KeyCode.DownArrow)) y++;
            if (Input.GetKey(controls[2]) || Input.GetKey(KeyCode.LeftArrow)) x--;
            if (Input.GetKey(controls[3]) || Input.GetKey(KeyCode.RightArrow)) x++;
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && root != null && screen == Screen.Playing) Show(Screen.Pause);
        }

        private void LoadStage(int index)
        {
            stageIndex = index;
            hintLevel = 0;
            simulation = new ReplaySimulation(ReplayStage.All[index]);
            attemptSeconds = 0; attemptRetries = 0;
            Show(Screen.Intro);
        }

        private void ClearStage()
        {
            int id = stageIndex + 1;
            clearedMask |= 1 << stageIndex;
            unlocked = Mathf.Max(unlocked, Mathf.Min(ReplayStage.All.Count, id + 1));
            PlayerPrefs.SetInt("rp.unlocked", unlocked);
            PlayerPrefs.SetInt("rp.cleared", clearedMask);
            float best = PlayerPrefs.GetFloat("rp.best." + id, float.MaxValue);
            if (attemptSeconds < best) PlayerPrefs.SetFloat("rp.best." + id, attemptSeconds);
            PlayerPrefs.SetInt("rp.lastRetries." + id, attemptRetries);
            PlayerPrefs.Save();
            Sound(740);
            Show(Screen.Clear);
        }

        private void ToggleRecord()
        {
            bool changed = simulation.Mode == ReplayMode.Recording ? simulation.Commit() : simulation.Begin();
            if (changed) { Sound(520); RefreshGame(); }
        }

        private void Retry()
        {
            if (screen == Screen.Clear) { attemptRetries = 0; attemptSeconds = 0; }
            else attemptRetries++;
            simulation.Retry();
            accumulator = 0;
            if (screen != Screen.Playing) Show(Screen.Playing); else RefreshGame();
        }

        private void Undo()
        {
            if (simulation.Mode == ReplayMode.Recording) simulation.Cancel();
            else simulation.Undo();
            accumulator = 0;
            RefreshGame();
        }

        private void Sound(float frequency)
        {
            if (volume <= 0) return;
            const int sampleRate = 22050;
            int count = sampleRate / 12;
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate) * (1f - i / (float)count) * volume * 0.12f;
            AudioClip clip = AudioClip.Create("ui tone", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
            Destroy(clip, 1f);
        }

        private void Flash(Color color, float tone)
        {
            flashUntil = Time.unscaledTime + .4f;
            if (flashLayer != null) flashLayer.style.backgroundColor = color;
            Sound(tone);
        }

        private void Show(Screen target)
        {
            screenChangedFrame = Time.frameCount;
            screen = target;
            accumulator = 0;
            nextUiRefresh = 0;
            boardContents = null;
            boardVeils = null;
            actorLayer = null;
            actorVisuals.Clear(); routeLayer = null; flashLayer = null; hurtUntil = 0; flashUntil = 0;
            grid = null;
            timeline = null;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);
            root.style.backgroundColor = Night;
            root.style.color = Ink;
            root.style.paddingLeft = 28;
            root.style.paddingRight = 28;
            root.style.paddingTop = 18;
            root.style.paddingBottom = 16;
            if (target == Screen.Title)
            {
                Texture2D art = Resources.Load<Texture2D>("DungeonTitle");
                if (art != null)
                {
                    var backdrop = new Image { image = art, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
                    backdrop.style.position = Position.Absolute;
                    backdrop.style.width = Length.Percent(100);
                    backdrop.style.height = Length.Percent(100);
                    root.Add(backdrop);
                }
            }
            BuildHeader();
            if (target == Screen.Playing) BuildGame();
            else if (target == Screen.Title) BuildTitle();
            else if (target == Screen.Intro) BuildIntro();
            else if (target == Screen.Pause) BuildPause();
            else if (target == Screen.Select) BuildSelect();
            else if (target == Screen.Help) BuildHelp();
            else if (target == Screen.Settings) BuildSettings();
            else if (target == Screen.Clear) BuildClear();
            else if (target == Screen.Failed) BuildFailure();
            else if (target == Screen.Credits) BuildCredits();
            else BuildComplete();
            BuildFooter();
        }

        private void BuildHeader()
        {
            VisualElement bar = Row();
            bar.style.height = 64;
            bar.style.alignItems = Align.Center;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.Add(Text("◈  REPLAY PARTNER", 22, Gold, true));
            bar.Add(Text("DUNGEON ESCAPE   /   10 CHAMBERS", 13, Ink));
            root.Add(bar);
        }

        private void BuildFooter()
        {
            VisualElement footer = Row();
            footer.style.height = 34;
            footer.style.alignItems = Align.FlexEnd;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.Add(Text("記録した足跡が、脱出への道になる。", 11, Muted));
            footer.Add(Text("© REPLAY PARTNER  •  YUKI TANAKA", 11, Muted));
            root.Add(footer);
        }

        private void BuildTitle()
        {
            VisualElement card = CenterCard();
            card.Add(Text("10の地下室から、地上へ。", 16, Mint, true));
            card.Add(Spacer(18));
            card.Add(Text("過去の自分と、\n迷宮を脱出しよう。", 46, Ink, true));
            card.Add(Spacer(18));
            card.Add(Text("目を覚ますと、石の牢の中。\n行動を記録し、過去の自分と仕掛けを解いて地上を目指す。", 20, Ink));
            card.Add(Spacer(20));
            card.Add(ActionButton("はじめる  →", () => LoadStage(0), true));
            card.Add(ActionButton("続きから", () => LoadStage(unlocked - 1)));
            card.Add(ActionButton("遊び方を見る", () => { previous = screen; Show(Screen.Help); }));
            card.Add(ActionButton("ステージを選ぶ", () => Show(Screen.Select)));
            var options = Row();
            foreach (var button in new[] {
                ActionButton("設定", () => { previous = screen; Show(Screen.Settings); }),
                ActionButton("クレジット", () => { previous = screen; Show(Screen.Credits); }),
                ActionButton("終了", () => Application.Quit()) })
            { button.style.flexGrow = 1; button.style.flexBasis = 0; options.Add(button); }
            card.Add(options);
            card.Add(Text("ENTER キーでも開始できます", 13, Muted));
        }

        private void BuildIntro()
        {
            ReplayStage stage = ReplayStage.All[stageIndex];
            VisualElement card = CenterCard();
            card.Add(Text($"STAGE {stage.Id:00}  /  10", 15, Mint, true));
            card.Add(Spacer(25));
            card.Add(Text(stage.Name, 44, Ink, true));
            card.Add(Spacer(14));
            card.Add(Text(stage.Intro, 24, Gold));
            card.Add(Spacer(34));
            card.Add(Text("今回の目標", 14, Muted, true));
            card.Add(Text(stage.Goal, 22, Ink, true));
            card.Add(Spacer(12));
            card.Add(Text("この部屋で覚えること：" + stage.Lesson, 16, Mint, true));
            if (stage.Id == 1) card.Add(Text($"{controls[0]}/{controls[1]}/{controls[2]}/{controls[3]}・矢印で歩く → 鍵に近づく → 出口へ", 16, Muted));
            if (stage.Id == 2) card.Add(Text($"{controls[4]}で記録 → Aへ移動 → {controls[4]}で確定 → 分身にAを任せて脱出", 16, Muted));
            if (stage.Id == 3) card.Add(Text("赤は作動中、予告が出たら離れる。安全な迂回路も使えます。", 16, Muted));
            card.Add(Spacer(28));
            card.Add(ActionButton("地下室に入る  →", () => Show(Screen.Playing), true));
            card.Add(Text("ENTER キーでも開始できます", 13, Muted));
        }

        private void BuildGame()
        {
            ReplayStage stage = ReplayStage.All[stageIndex];
            VisualElement body = Row();
            body.style.flexGrow = 1;
            body.style.marginTop = 10;
            root.Add(body);

            VisualElement boardSide = Panel();
            boardSide.style.flexGrow = 1;
            boardSide.style.marginRight = 18;
            boardSide.style.paddingLeft = 20;
            boardSide.style.paddingRight = 20;
            boardSide.style.paddingTop = 18;
            body.Add(boardSide);
            VisualElement top = Row();
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            top.Add(Text($"{stage.Id:00} / 10  {stage.Theme} — {stage.Name}", 17, Ink, true));
            statusText = Text("", 14, Mint, true);
            top.Add(statusText);
            boardSide.Add(top);
            boardSide.Add(Spacer(15));
            grid = new VisualElement();
            grid.style.height = simulation.HasTraps ? 355 : 383;
            grid.style.flexDirection = FlexDirection.Column;
            grid.style.borderTopWidth = grid.style.borderBottomWidth = grid.style.borderLeftWidth = grid.style.borderRightWidth = 1;
            grid.style.borderTopColor = grid.style.borderBottomColor = grid.style.borderLeftColor = grid.style.borderRightColor = Line;
            grid.style.borderTopLeftRadius = grid.style.borderTopRightRadius = grid.style.borderBottomLeftRadius = grid.style.borderBottomRightRadius = 6;
            grid.style.overflow = Overflow.Hidden;
            boardSide.Add(grid);
            boardSide.Add(Spacer(13));
            feedbackText = Text("", 15, Gold);
            boardSide.Add(feedbackText);
            boardSide.Add(Spacer(10));
            boardSide.Add(Text("金の人影 = あなた   青い人影 = 分身   金の鍵 = 脱出鍵   A/B = 床スイッチ", 13, Muted));
            boardSide.Add(Spacer(20));
            boardSide.Add(Text("REPLAY TIMELINE   /   分身の足跡と再生時間", 12, Mint, true));
            timeline = new VisualElement();
            timeline.style.marginTop = 8;
            boardSide.Add(timeline);

            VisualElement side = Panel();
            side.style.width = 306;
            side.style.paddingLeft = 22;
            side.style.paddingRight = 22;
            side.style.paddingTop = 18;
            side.style.borderTopColor = Gold;
            side.style.borderTopWidth = 3;
            body.Add(side);
            side.Add(Text("現在の目標", 13, Mint, true));
            side.Add(Spacer(8));
            side.Add(Text(stage.Goal, 21, Ink, true));
            side.Add(Spacer(12));
            side.Add(Text("次にすること", 13, Muted, true));
            guideText = Text("", 18, Gold, true);
            side.Add(guideText);
            side.Add(Spacer(12));
            partnerText = Text("", 15, Muted);
            side.Add(partnerText);
            side.Add(Spacer(10));
            recordButton = ActionButton("", ToggleRecord, true);
            recordButton.tooltip = "E：行動を最大30秒記録。確定すると分身が同じ行動を再生します。";
            side.Add(recordButton);
            side.Add(ActionButton("リトライ   " + controls[5], Retry));
            undoButton = ActionButton("分身を削除   Backspace", Undo);
            undoButton.tooltip = "最後に確定した分身を取り消し、部屋を巻き戻します。";
            side.Add(undoButton);
            side.Add(ActionButton("?  ヒントを見る", () => { hintLevel = Mathf.Min(3, hintLevel + 1); RefreshGame(); }));
            side.Add(ActionButton("一時停止   ESC", () => Show(Screen.Pause)));
            side.Add(Spacer(8));
            side.Add(Text($"移動 {controls[0]}/{controls[1]}/{controls[2]}/{controls[3]}・矢印\n記録 {controls[4]} / Tab 足跡の切替", 13, Muted));
            side.Add(ActionButton("遊び方   F1", () => { previous = screen; Show(Screen.Help); }));
            RefreshGame();
        }

        private void RefreshGame()
        {
            if (screen != Screen.Playing || grid == null) return;
            statusText.text = simulation.Mode == ReplayMode.Recording ? "● 行動を記録中" : simulation.RecordingsCount > 0 ? "▶ 分身を再生中" : "探索中";
            statusText.style.color = simulation.Mode == ReplayMode.Recording ? Gold : Mint;
            partnerText.style.color = simulation.Health == 1 ? (Color)new Color32(255, 151, 126, 255) : Muted;
            partnerText.text = $"体力  {new string('♥', simulation.Health)}  {simulation.Health}/3   /   分身 {simulation.RecordingsCount}/2" +
                (simulation.NeedsKey ? $"\n脱出の鍵  {(simulation.HasKey ? "入手済み" : "未入手")}" : "") +
                (Array.Exists(simulation.Stage.Map, row => row.IndexOf('a') >= 0) ? $"\n門： A {(simulation.OpenA ? "開" : "閉")}" : "") +
                (Array.Exists(simulation.Stage.Map, row => row.IndexOf('b') >= 0) ? $"   B {(simulation.OpenB ? "開" : "閉")}" : "");
            recordButton.text = stageIndex == 0 ? "記録は次の部屋で解放" :
                simulation.Mode == ReplayMode.Recording ? $"記録を確定 {controls[4]} / 残り {simulation.Remaining / 60f:0.0}s" : "記録を始める   " + controls[4];
            recordButton.SetEnabled(stageIndex > 0 &&
                (simulation.Mode == ReplayMode.Recording || simulation.RecordingsCount < 2));
            undoButton.text = simulation.Mode == ReplayMode.Recording ? "記録を取り消す   " + controls[6] : "分身を削除   " + controls[6];
            undoButton.tooltip = simulation.Mode == ReplayMode.Recording ? "今回の記録だけを中止。確定済みの分身は残ります。" : "最後の分身を取り消して部屋を巻き戻します。";
            undoButton.SetEnabled(simulation.Mode == ReplayMode.Recording || simulation.RecordingsCount > 0);
            guideText.text = NextAction();
            string message = !string.IsNullOrEmpty(simulation.Feedback) ? simulation.Feedback :
                hintLevel > 0 ? $"ヒント {hintLevel}/3  {ReplayStage.All[stageIndex].Hints[hintLevel - 1]}" : ReplayStage.All[stageIndex].Intro;
            string trap = simulation.TrapsActive ? "作動中・触れないで" : simulation.TrapsWarning ? "まもなく作動・離れよう" : "休止中・通行できる";
            message = message.Replace("E で", controls[4] + " で").Replace("E、", controls[4] + "、");
            feedbackText.text = simulation.HasTraps ? $"床の罠：{trap}  /  切替まで {simulation.TrapCountdown:0.0} 秒\n{message}" : message;
            feedbackText.style.color = simulation.HasTraps && (simulation.TrapsActive || simulation.TrapsWarning) ? (Color)new Color32(255, 174, 137, 255) : Gold;
            DrawBoard();
            DrawTimeline();
        }

        private void DrawTimeline()
        {
            timeline.Clear();
            if (simulation.RecordingsCount == 0)
            {
                timeline.Add(Text("分身を記録すると、ここに再生時間が表示されます。", 13, Muted));
                return;
            }
            for (int i = 0; i < simulation.RecordingsCount; i++)
            {
                int length = simulation.RecordingLength(i);
                VisualElement lane = Row();
                lane.style.alignItems = Align.Center;
                lane.style.marginBottom = 5;
                string state = simulation.IsCloneDone(i) ? "停止中" : simulation.Clones[i].Blocked ? "通行待ち" : "再生中";
                Label label = Text($"分身{i + 1} {state}  {Mathf.Min(simulation.Tick, length) / 60f:0.0}s", 12, Muted);
                label.style.width = 175;
                lane.Add(label);
                VisualElement track = new VisualElement();
                track.style.flexGrow = 1;
                track.style.height = 7;
                track.style.backgroundColor = Tile;
                VisualElement fill = new VisualElement();
                fill.style.width = Length.Percent(length == 0 ? 100 : Mathf.Min(100f, simulation.Tick * 100f / length));
                fill.style.height = 7;
                fill.style.backgroundColor = i == 0 ? Blue : Mint;
                track.Add(fill);
                lane.Add(track);
                timeline.Add(lane);
            }
        }

        private string NextAction()
        {
            if (stageIndex == 0) return simulation.HasKey ? "中央の通路へ戻り、右の出口へ" : "右下の床にある金色の鍵を探そう";
            if (simulation.Mode == ReplayMode.Recording) return $"スイッチを踏んだら {controls[4]} で確定。{controls[6]} で取り消せます";
            bool needsB = Array.Exists(simulation.Stage.Map, row => row.IndexOf('b') >= 0);
            if (simulation.OpenA && (!needsB || simulation.OpenB))
                return simulation.NeedsKey && !simulation.HasKey ? "扉が開いた！右上の鍵を拾って出口へ" : "必要な扉が開いた！出口へ進もう";
            if (stageIndex == 3) return "石箱を押して A の床スイッチに載せよう";
            if (simulation.RecordingsCount == 0) return $"{controls[4]} で記録を始め、分身にスイッチを任せよう";
            if (!simulation.OpenA)
                return simulation.IsCloneDone(0) ? $"分身が A に届いていません。{controls[6]} で記録を見直そう" : "分身が A に向かっています。足跡と門を確認しよう";
            if (simulation.RecordingsCount == 1 && stageIndex >= 5 && simulation.Crates.Count == 0)
                return $"二つ目のスイッチも必要。もう一度 {controls[4]} で記録";
            if (simulation.NeedsKey && !simulation.HasKey && simulation.RecordingsCount == 2)
                return "二つの扉を抜け、右上の鍵を拾おう";
            return "分身と協力して扉を開き、出口へ進もう";
        }

        private void BuildBoard()
        {
            var stage = simulation.Stage;
            int rows = stage.Map.Length, columns = stage.Map[0].Length;
            boardContents = new VisualElement[columns, rows];
            boardVeils = new VisualElement[columns, rows];
            for (int y = 0; y < rows; y++)
            {
                var row = Row();
                row.style.flexGrow = 1; row.style.flexBasis = 0; row.style.minHeight = 0;
                grid.Add(row);
                for (int x = 0; x < columns; x++)
                {
                    bool wall = stage.Map[y][x] == '#';
                    var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                    cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.minWidth = 0;
                    cell.style.backgroundColor = wall ? Night : Tile;
                    if (wall) { cell.style.borderTopWidth = 2; cell.style.borderTopColor = Line; }
                    Texture2D texture = wall ? wallArt : floorArt;
                    if (texture != null)
                    {
                        var image = new Image { image = texture, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
                        image.style.position = Position.Absolute;
                        image.style.width = Length.Percent(100); image.style.height = Length.Percent(100);
                        cell.Add(image);
                    }
                    var veil = new VisualElement { pickingMode = PickingMode.Ignore };
                    veil.style.position = Position.Absolute;
                    veil.style.width = Length.Percent(100); veil.style.height = Length.Percent(100);
                    cell.Add(veil); boardVeils[x, y] = veil;
                    var atmosphere = new VisualElement { pickingMode = PickingMode.Ignore };
                    atmosphere.style.position = Position.Absolute;
                    atmosphere.style.width = atmosphere.style.height = Length.Percent(100);
                    atmosphere.style.backgroundColor = stage.Ambient;
                    cell.Add(atmosphere);
                    var content = new VisualElement { pickingMode = PickingMode.Ignore };
                    content.style.position = Position.Absolute;
                    content.style.width = Length.Percent(100); content.style.height = Length.Percent(100);
                    content.style.alignItems = Align.Center; content.style.justifyContent = Justify.Center;
                    cell.Add(content); boardContents[x, y] = content;
                    row.Add(cell);
                }
            }
            routeLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            routeLayer.style.position = Position.Absolute;
            routeLayer.style.width = routeLayer.style.height = Length.Percent(100);
            routeLayer.style.display = showRoutes ? DisplayStyle.Flex : DisplayStyle.None;
            grid.Add(routeLayer);
            actorLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            actorLayer.style.position = Position.Absolute;
            actorLayer.style.width = Length.Percent(100); actorLayer.style.height = Length.Percent(100);
            grid.Add(actorLayer);
            flashLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            flashLayer.style.position = Position.Absolute;
            flashLayer.style.width = flashLayer.style.height = Length.Percent(100);
            flashLayer.style.opacity = 0;
            grid.Add(flashLayer);
        }

        private void DrawBoard()
        {
            if (boardContents == null) BuildBoard();
            var stage = simulation.Stage;
            for (int y = 0; y < stage.Map.Length; y++)
                for (int x = 0; x < stage.Map[y].Length; x++)
                {
                    var cell = boardContents[x, y];
                    cell.Clear();
                    char symbol = stage.Map[y][x];
                    var at = new Vector2Int(x, y);
                    float distance = Vector2.Distance(simulation.Player, at);
                    Color color = new Color(0, 0, 0, Mathf.Clamp(0.12f + distance * 0.032f, 0.12f, 0.55f));
                    if (symbol == 'A' || symbol == 'a') color = new Color(0.03f, 0.4f, 0.3f, simulation.OpenA ? 0.45f : 0.24f);
                    if (symbol == 'B' || symbol == 'b') color = new Color(0.4f, 0.24f, 0.05f, simulation.OpenB ? 0.45f : 0.24f);
                    if (symbol == 'E') color = new Color(0.4f, 0.32f, 0.05f, 0.42f);
                    boardVeils[x, y].style.backgroundColor = color;
                    if (symbol == 'A' || symbol == 'B')
                    {
                        Color rune = symbol == 'A' ? Mint : Gold;
                        var plate = new VisualElement();
                        plate.style.position = Position.Absolute;
                        plate.style.left = Length.Percent(15); plate.style.top = Length.Percent(15);
                        plate.style.width = Length.Percent(70); plate.style.height = Length.Percent(70);
                        plate.style.alignItems = Align.Center; plate.style.justifyContent = Justify.Center;
                        plate.style.borderTopLeftRadius = 24; plate.style.borderTopRightRadius = 24;
                        plate.style.borderBottomLeftRadius = 24; plate.style.borderBottomRightRadius = 24;
                        plate.style.borderTopWidth = 2; plate.style.borderBottomWidth = 2;
                        plate.style.borderLeftWidth = 2; plate.style.borderRightWidth = 2;
                        plate.style.borderTopColor = rune; plate.style.borderBottomColor = rune;
                        plate.style.borderLeftColor = rune; plate.style.borderRightColor = rune;
                        if (simulation.Occupied(at)) { plate.style.backgroundColor = new Color(rune.r, rune.g, rune.b, .6f); plate.transform.scale = new Vector3(.85f, .85f, 1); plate.transform.position = new Vector3(0, 3, 0); }
                        plate.Add(Text(symbol.ToString(), 18, rune, true)); cell.Add(plate);
                    }
                    if (symbol == 'K' && !simulation.HasKey) AddArt(cell, keyArt, "鍵", Gold, 76);
                    if (symbol == 'E') AddArt(cell, simulation.NeedsKey && !simulation.HasKey ? gateArt : exitArt, "出口", Gold, 92);
                    if (symbol == 'a' || symbol == 'b')
                    {
                        float elapsed = Time.unscaledTime - (symbol == 'a' ? gateAChanged : gateBChanged);
                        bool opened = simulation.IsOpen(symbol);
                        if (!opened) AddArt(cell, gateArt, "門", Gold, 92);
                        else
                        {
                            cell.Add(Text("開", 17, symbol == 'a' ? Mint : Gold, true));
                            if (!reducedMotion && elapsed < .5f && gateArt != null)
                            {
                                var gate = Art(gateArt, 92); gate.transform.position = new Vector3(0, -elapsed * 100, 0);
                                gate.style.opacity = 1f - elapsed * 2; cell.Add(gate);
                            }
                        }
                    }
                    if (symbol == 'T')
                    {
                        Color danger = simulation.TrapsActive ? (Color)new Color32(245, 87, 75, 255) : simulation.TrapsWarning ? Gold : Muted;
                        boardVeils[x, y].style.backgroundColor = new Color(danger.r, danger.g, danger.b, simulation.TrapsActive ? 0.48f : 0.2f);
                        cell.Add(Text(simulation.TrapsActive ? "▲▲" : simulation.TrapsWarning ? "⚠" : "···", 21, danger, true));
                    }
                    if (symbol == '#' && y == 0 && (x == 4 || x == 11)) cell.Add(Text("✦", 22, Gold, true));
                    if (symbol == '.' && stage.Id >= 4 && stage.Id <= 6 && y == 5 && x % 3 == 0) cell.Add(Text("≈", 20, Blue));
                    if (symbol == '.' && stage.Id >= 7 && stage.Id <= 9 && y == 4 && x % 4 == 0) cell.Add(Text("◆", 12, Muted));
                    foreach (var crate in simulation.Crates) if (crate == at) AddArt(cell, crateArt, "石箱", Gold, 86);
                }
            DrawActors();
        }

        private void DrawRoutes()
        {
            if (routeLayer == null) return;
            routeLayer.Clear();
            for (int i = 0; i < simulation.RecordingsCount; i++)
            {
                var path = simulation.RecordedPath(i);
                Vector2 previousPoint = new Vector2(-100, -100);
                for (int n = 0; n < path.Count; n += 12)
                {
                    if (Vector2.Distance(previousPoint, path[n]) < .2f) continue;
                    var dot = Text("•", 15, i == 0 ? Blue : Mint, true);
                    dot.pickingMode = PickingMode.Ignore; dot.style.position = Position.Absolute;
                    dot.style.left = Length.Percent((path[n].x + .35f) * 100f / simulation.Stage.Map[0].Length);
                    dot.style.top = Length.Percent((path[n].y + .25f) * 100f / simulation.Stage.Map.Length);
                    dot.style.opacity = .45f; routeLayer.Add(dot); previousPoint = path[n];
                }
                if (path.Count == 0) continue;
                Vector2 end = path[path.Count - 1];
                var stop = Text("停止 " + (i + 1), 11, i == 0 ? Blue : Mint, true);
                stop.pickingMode = PickingMode.Ignore; stop.style.position = Position.Absolute;
                stop.style.left = Length.Percent((end.x + .15f) * 100f / simulation.Stage.Map[0].Length);
                stop.style.top = Length.Percent((end.y + .8f) * 100f / simulation.Stage.Map.Length);
                routeLayer.Add(stop);
            }
        }

        private void DrawActors()
        {
            if (actorLayer == null) return;
            int count = simulation.Clones.Count + 1;
            if (actorVisuals.Count != count)
            {
                actorLayer.Clear(); actorVisuals.Clear(); DrawRoutes();
                for (int i = 0; i < count; i++)
                {
                    bool human = i == count - 1;
                    Color aura = human ? Gold : Blue;
                    var actor = new VisualElement { pickingMode = PickingMode.Ignore };
                    actor.style.position = Position.Absolute;
                    actor.style.width = Length.Percent(90f / simulation.Stage.Map[0].Length);
                    actor.style.height = Length.Percent(90f / simulation.Stage.Map.Length);
                    actor.style.borderTopLeftRadius = 26; actor.style.borderTopRightRadius = 26;
                    actor.style.borderBottomLeftRadius = 26; actor.style.borderBottomRightRadius = 26;
                    actor.style.backgroundColor = new Color(aura.r, aura.g, aura.b, human ? 0.3f : 0.48f);
                    actor.style.borderTopWidth = actor.style.borderBottomWidth = actor.style.borderLeftWidth = actor.style.borderRightWidth = 2;
                    actor.style.borderTopColor = actor.style.borderBottomColor = actor.style.borderLeftColor = actor.style.borderRightColor = aura;
                    var motion = new ActorMotion
                    {
                        Previous = human ? simulation.Player : simulation.Clones[i].Position,
                        Shadow = new VisualElement { pickingMode = PickingMode.Ignore }
                    };
                    motion.Shadow.style.position = Position.Absolute;
                    motion.Shadow.style.left = Length.Percent(10);
                    motion.Shadow.style.top = Length.Percent(72);
                    motion.Shadow.style.width = Length.Percent(80);
                    motion.Shadow.style.height = Length.Percent(28);
                    motion.Shadow.style.backgroundColor = new Color(0, 0, 0, .55f);
                    motion.Shadow.style.borderTopLeftRadius = motion.Shadow.style.borderTopRightRadius = 50;
                    motion.Shadow.style.borderBottomLeftRadius = motion.Shadow.style.borderBottomRightRadius = 50;
                    actor.Add(motion.Shadow);
                    motion.Puppet = new DungeonPuppet(human ? new Color32(72, 100, 94, 255) : new Color32(77, 145, 186, 255));
                    actor.Add(motion.Puppet.Root);
                    actor.userData = motion;
                    actorLayer.Add(actor); actorVisuals.Add(actor);
                }
            }
            for (int i = 0; i < count; i++)
            {
                Vector2 position = i == count - 1 ? simulation.Player : simulation.Clones[i].Position;
                actorVisuals[i].style.left = Length.Percent((position.x + 0.05f) * 100f / simulation.Stage.Map[0].Length);
                actorVisuals[i].style.top = Length.Percent((position.y + 0.05f) * 100f / simulation.Stage.Map.Length);
                var motion = (ActorMotion)actorVisuals[i].userData;
                Vector2 delta = position - motion.Previous;
                // Only visual transforms: collision and recorded inputs remain untouched.
                bool moved = delta.sqrMagnitude > .000001f && delta.sqrMagnitude < 1f;
                if (moved) { motion.Stride += delta.magnitude * 10f; motion.LastMoved = Time.unscaledTime; }
                if (delta.sqrMagnitude >= 1f) motion.LastMoved = -1;
                bool walking = Time.unscaledTime - motion.LastMoved < .07f;
                if (moved && Mathf.Abs(delta.x) > .001f) motion.Facing = delta.x > 0 ? 1 : -1;
                if (moved && Mathf.Abs(delta.y) > .001f) motion.BackFacing = delta.y < 0;
                float wave = walking ? Mathf.Sin(motion.Stride) : Mathf.Sin(Time.unscaledTime * 2f) * .12f;
                motion.Puppet.Animate(reducedMotion ? 0 : motion.Stride, walking && !reducedMotion, motion.Facing, i == count - 1 && Time.unscaledTime < hurtUntil && !reducedMotion, motion.BackFacing);
                motion.Shadow.transform.scale = new Vector3(1f - Mathf.Abs(wave) * .15f, 1, 1);
                motion.Previous = position;
            }
        }

        private static Image Art(Texture2D texture, int percent)
        {
            var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.style.position = Position.Absolute;
            image.style.left = Length.Percent((100 - percent) / 2f);
            image.style.top = Length.Percent((100 - percent) / 2f);
            image.style.width = Length.Percent(percent);
            image.style.height = Length.Percent(percent);
            return image;
        }

        private static void AddArt(VisualElement cell, Texture2D texture, string fallback, Color color, int size)
        {
            if (texture != null) cell.Add(Art(texture, size));
            else cell.Add(Text(fallback, 18, color, true));
        }

        private void BuildPause()
        {
            VisualElement card = CenterCard();
            card.Add(Text("一時停止", 42, Ink, true));
            card.Add(Text("ここまでの記録は残っています。", 19, Muted));
            card.Add(Spacer(28));
            card.Add(ActionButton("ゲームに戻る", () => Show(Screen.Playing), true));
            card.Add(ActionButton("この部屋をやり直す", Retry));
            card.Add(ActionButton("遊び方", () => { previous = screen; Show(Screen.Help); }));
            card.Add(ActionButton("設定", () => { previous = screen; Show(Screen.Settings); }));
            card.Add(ActionButton("タイトルへ", () => Show(Screen.Title)));
        }

        private void BuildSelect()
        {
            VisualElement card = CenterCard();
            card.style.width = 860;
            card.Add(Text("ステージ選択", 35, Ink, true));
            card.Add(Text("クリアした部屋には、いつでも戻れます。", 17, Muted));
            card.Add(Spacer(18));
            for (int row = 0; row < 2; row++)
            {
                VisualElement line = Row();
                for (int column = 0; column < 5; column++)
                {
                    int index = row * 5 + column;
                    ReplayStage stage = ReplayStage.All[index];
                    string state = (clearedMask & (1 << index)) != 0 ? "✓" : index < unlocked ? "→" : "×";
                    float best = PlayerPrefs.GetFloat("rp.best." + stage.Id, 0);
                    Button choice = ActionButton($"{stage.Id:00} {state}\n{stage.Name}\n" + (best > 0 ? $"BEST {best:0.0}s" : "記録なし"), () => LoadStage(index));
                    choice.style.fontSize = 13;
                    choice.style.width = 150;
                    choice.style.height = 86;
                    choice.style.marginRight = 8;
                    choice.SetEnabled(index < unlocked);
                    line.Add(choice);
                }
                card.Add(line);
            }
            card.Add(Spacer(20));
            card.Add(ActionButton("タイトルへ戻る", () => Show(Screen.Title)));
        }

        private void BuildHelp()
        {
            VisualElement card = CenterCard();
            card.style.width = 740;
            card.Add(Text("遊び方", 40, Ink, true));
            card.Add(Text("目的：仕掛けを解き、地下迷宮から脱出する", 22, Gold, true));
            card.Add(Text("以下は初期キーの説明です。変更後のキーは下欄とゲーム画面で確認できます。", 13, Muted));
            card.Add(Spacer(20));
            card.Add(Text("1  WASD / 矢印キーで移動。金色の鍵がある部屋では先に拾います。\n\n2  石箱は隣から押せます。床スイッチ A/B に載せることもできます。\n\n3  E で行動を記録・確定。部屋が巻き戻り、青い分身が同じ動きを再生します。\n\n4  分身にスイッチを任せ、開いた扉の先の出口へ進みます。", 18, Ink));
            card.Add(Spacer(25));
            card.Add(Text($"リトライ {controls[5]} / 取消・分身削除 {controls[6]} / Esc 一時停止 / Tab 足跡", 15, Muted));
            card.Add(Text($"現在の設定：移動 {controls[0]}/{controls[1]}/{controls[2]}/{controls[3]}・矢印、記録 {controls[4]}。\n足跡は記録時の経路です。箱や門が変わると再生はずれることがあります。", 14, Muted));
            card.Add(Text("斜めにも自由に歩けます。罠は赤で作動、金色で予告。\n体力は3回分。倒れても分身の記録を残して再挑戦できます。", 16, Gold));
            card.Add(ActionButton("戻る", () => Show(previous), true));
        }

        private void BuildSettings()
        {
            VisualElement card = CenterCard();
            card.style.width = 800;
            card.Add(Text("設定", 40, Ink, true));
            card.Add(Text("効果音の音量", 19, Muted));
            Label value = Text($"{Mathf.RoundToInt(volume * 100)}%", 24, Gold, true);
            VisualElement controls = Row();
            controls.style.alignItems = Align.Center;
            controls.Add(ActionButton("−", () => ChangeVolume(-0.1f)));
            controls.Add(value);
            controls.Add(ActionButton("＋", () => ChangeVolume(0.1f)));
            card.Add(controls);
            var motionToggle = new Toggle("演出を弱める（点滅・揺れを抑える）") { value = reducedMotion };
            motionToggle.style.color = Ink; motionToggle.style.fontSize = 17;
            motionToggle.RegisterValueChangedCallback(evt => { reducedMotion = evt.newValue; PlayerPrefs.SetInt("rp.reducedMotion", reducedMotion ? 1 : 0); PlayerPrefs.Save(); });
            card.Add(motionToggle);
            VisualElement windows = Row();
            windows.Add(ActionButton("1280 × 800", () => UnityEngine.Screen.SetResolution(1280, 800, FullScreenMode.Windowed)));
            windows.Add(ActionButton("1600 × 900", () => UnityEngine.Screen.SetResolution(1600, 900, FullScreenMode.Windowed)));
            card.Add(windows);
            card.Add(Text(bindingMessage + (pendingBinding >= 0 ? "  Esc：中止" : ""), 15, Gold));
            for (int row = 0; row < 2; row++)
            {
                var line = Row();
                for (int column = 0; column < 4; column++)
                {
                    int index = row * 4 + column; if (index >= this.controls.Length) break;
                    var button = ActionButton(controlNames[index] + "：" + this.controls[index], () => { pendingBinding = index; bindingMessage = controlNames[index] + "に割り当てるキーを押してください。"; Show(Screen.Settings); });
                    button.style.flexGrow = 1; button.style.fontSize = 13; line.Add(button);
                }
                card.Add(line);
            }
            card.Add(Text("矢印キーは常に移動。Tab：足跡表示切替 / F1：遊び方。", 14, Muted));
            card.Add(ActionButton("キー設定を初期値に戻す", () =>
            {
                Array.Copy(ReplayControls.Defaults, this.controls, this.controls.Length);
                for (int i = 0; i < this.controls.Length; i++) PlayerPrefs.SetInt("rp.key." + i, (int)this.controls[i]);
                PlayerPrefs.Save(); pendingBinding = -1; bindingMessage = "初期キーに戻しました。"; Show(Screen.Settings);
            }));
            card.Add(ActionButton("戻る", () => { pendingBinding = -1; Show(previous); }, true));
        }

        private void ChangeVolume(float delta)
        {
            volume = Mathf.Clamp01(Mathf.Round((volume + delta) * 10f) / 10f);
            PlayerPrefs.SetFloat("rp.volume", volume);
            PlayerPrefs.Save();
            Show(Screen.Settings);
            Sound(520);
        }

        private void BuildCredits()
        {
            var card = CenterCard();
            card.Add(Text("REPLAY PARTNER", 34, Gold, true));
            card.Add(Text("制作：Yuki Tanaka", 22, Ink, true));
            card.Add(Spacer(18));
            card.Add(Text("Unity 6 / C# / UI Toolkit\n企画・改善方針の検討に加え、実装・検証・文章作成にAI（Codex）の支援を使用。", 17, Ink));
            card.Add(Spacer(16));
            card.Add(Text("背景等の画像8点：本作用にOpenAI画像生成で作成\nキャラ：独立したUIパーツによる描画・歩行\n効果音：コードで生成\n日本語フォント：Noto CJK / SIL OFL 1.1\n配布フォルダにライセンス・技術説明・検証記録を同梱。", 16, Muted));
            card.Add(Spacer(20));
            card.Add(ActionButton("戻る", () => Show(previous), true));
        }

        private void BuildFailure()
        {
            VisualElement card = CenterCard();
            card.Add(Text("迷宮に倒れても、足跡は残る。", 32, Gold, true));
            card.Add(Text("床の罠は赤で作動、金色で予告。安全な迂回路も探してみよう。\n分身の記録を残したまま、もう一度挑戦できます。", 19, Ink));
            card.Add(Text($"今回の挑戦  {attemptSeconds:0.0} 秒 / 分身 {simulation.RecordingsCount} 体 / 再挑戦 {attemptRetries} 回", 16, Muted));
            card.Add(Spacer(24));
            card.Add(ActionButton("記録を残して再挑戦", Retry, true));
            card.Add(ActionButton("タイトルへ", () => Show(Screen.Title)));
        }

        private void BuildClear()
        {
            VisualElement card = CenterCard();
            card.Add(Text($"STAGE {stageIndex + 1:00} CLEAR", 18, Mint, true));
            card.Add(Spacer(16));
            card.Add(Text("扉の向こうへ。", 43, Ink, true));
            card.Add(Text(ReplayStage.All[stageIndex].Intro, 20, Gold));
            card.Add(Spacer(24));
            card.Add(Text($"挑戦時間  {attemptSeconds:0.0} 秒   /   分身 {simulation.RecordingsCount} 体   /   再挑戦 {attemptRetries} 回" +
                (simulation.NeedsKey ? "   /   鍵を回収" : ""), 17, Muted));
            card.Add(Text($"この部屋の最短記録  {PlayerPrefs.GetFloat("rp.best." + (stageIndex + 1), attemptSeconds):0.0} 秒（記録作成・やり直しを含む）", 15, Gold));
            card.Add(Spacer(20));
            card.Add(ActionButton(stageIndex == ReplayStage.All.Count - 1 ? "エピローグへ" : "次のステージへ", () =>
            {
                if (stageIndex == ReplayStage.All.Count - 1) Show(Screen.Complete);
                else LoadStage(stageIndex + 1);
            }, true));
            card.Add(ActionButton("この部屋をもう一度", Retry));
            card.Add(ActionButton("ステージ選択", () => Show(Screen.Select)));
        }

        private void BuildComplete()
        {
            VisualElement card = CenterCard();
            card.Add(Text("地下迷宮のすべての部屋を突破", 18, Mint, true));
            card.Add(Spacer(18));
            card.Add(Text("脱出成功。", 49, Ink, true));
            card.Add(Spacer(20));
            card.Add(Text("「もうひとりの自分がいれば」と思った日があった。\nでも、ここまで連れてきたのは、ずっとあなた自身だった。", 23, Gold));
            float total = 0; int measured = 0;
            for (int i = 1; i <= 10; i++) if (PlayerPrefs.HasKey("rp.best." + i)) { total += PlayerPrefs.GetFloat("rp.best." + i); measured++; }
            card.Add(Text($"保存した最短記録：{measured}/10 部屋  合計 {total:0.0} 秒\n記録作成を含む各部屋のベストタイム合計です。", 16, Muted));
            card.Add(Spacer(30));
            card.Add(ActionButton("ステージを選ぶ", () => Show(Screen.Select), true));
            card.Add(ActionButton("タイトルへ", () => Show(Screen.Title)));
        }

        private VisualElement CenterCard()
        {
            VisualElement space = new VisualElement();
            space.style.flexGrow = 1;
            space.style.alignItems = Align.Center;
            space.style.justifyContent = Justify.Center;
            root.Add(space);
            VisualElement card = Panel();
            card.style.width = 650;
            card.style.paddingLeft = 42;
            card.style.paddingRight = 42;
            card.style.paddingTop = 32;
            card.style.paddingBottom = 32;
            space.Add(card);
            return card;
        }

        private static VisualElement Panel()
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = Surface;
            panel.style.borderTopWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderTopColor = Line;
            panel.style.borderBottomColor = Line;
            panel.style.borderLeftColor = Line;
            panel.style.borderRightColor = Line;
            panel.style.borderTopLeftRadius = 12;
            panel.style.borderTopRightRadius = 12;
            panel.style.borderBottomLeftRadius = 12;
            panel.style.borderBottomRightRadius = 12;
            return panel;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static VisualElement Spacer(float height)
        {
            var space = new VisualElement();
            space.style.height = height;
            return space;
        }

        private static Label Text(string value, int size, Color color, bool bold = false)
        {
            var label = new Label(value);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Button ActionButton(string value, Action action, bool primary = false)
        {
            var button = new Button(action) { text = value };
            button.style.height = 40;
            button.style.marginTop = 8;
            button.style.paddingLeft = 14;
            button.style.paddingRight = 14;
            button.style.borderTopLeftRadius = 7;
            button.style.borderTopRightRadius = 7;
            button.style.borderBottomLeftRadius = 7;
            button.style.borderBottomRightRadius = 7;
            button.style.backgroundColor = primary ? Gold : Tile;
            button.style.color = primary ? Night : Ink;
            button.style.fontSize = 16;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            Color resting = primary ? Gold : Tile;
            Color highlighted = primary ? new Color32(255, 217, 153, 255) : new Color32(57, 79, 85, 255);
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 1;
            button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = primary ? Gold : Line;
            button.RegisterCallback<PointerEnterEvent>(_ => { if (button.enabledInHierarchy) button.style.backgroundColor = highlighted; });
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = resting);
            button.RegisterCallback<FocusInEvent>(_ => button.style.backgroundColor = highlighted);
            button.RegisterCallback<FocusOutEvent>(_ => button.style.backgroundColor = resting);
            return button;
        }
    }
}
