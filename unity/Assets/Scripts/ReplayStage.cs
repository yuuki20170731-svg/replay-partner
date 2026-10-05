using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReplayPartner
{
    public sealed class ReplayStage
    {
        public readonly int Id;
        public readonly string Name;
        public readonly string Goal;
        public readonly string Lesson;
        public readonly string Intro;
        public readonly string[] Hints;
        public readonly string[] Map;
        public string Theme => Id <= 3 ? "地下牢" : Id <= 6 ? "忘れられた水路" : Id <= 9 ? "崩れた回廊" : "地上への門";
        public Color Ambient => Id <= 3 ? new Color(.12f, .08f, .02f, .12f) : Id <= 6 ? new Color(.02f, .28f, .4f, .2f) : Id <= 9 ? new Color(.35f, .12f, .04f, .16f) : new Color(.5f, .35f, .05f, .2f);

        public ReplayStage(int id, string name, string goal, string lesson, string intro, string[] hints, string[] map)
        {
            Id = id;
            Name = name;
            Goal = goal;
            Lesson = lesson;
            Intro = intro;
            Hints = hints;
            Map = map;
        }

        private static ReplayStage Make(int id, string name, string goal, string lesson, string intro,
            string[] hints, Vector2Int? a = null, Vector2Int? b = null, int barriers = 0,
            Vector2Int? crate = null, Vector2Int? exit = null, Vector2Int? key = null)
        {
            char[][] map = new char[7][];
            for (int y = 0; y < 7; y++)
            {
                map[y] = new char[16];
                for (int x = 0; x < 16; x++)
                    map[y][x] = x == 0 || x == 15 || y == 0 || y == 6 ? '#' : '.';
            }
            map[3][2] = 'P';
            Vector2Int end = exit ?? new Vector2Int(13, 3);
            map[end.y][end.x] = 'E';
            for (int i = 0; i < barriers; i++)
            {
                int x = i == 0 ? 7 : 11;
                for (int y = 1; y <= 5; y++) map[y][x] = '#';
                map[3][x] = i == 0 ? 'a' : 'b';
            }
            if (a.HasValue) map[a.Value.y][a.Value.x] = 'A';
            if (b.HasValue) map[b.Value.y][b.Value.x] = 'B';
            if (crate.HasValue) map[crate.Value.y][crate.Value.x] = 'C';
            if (key.HasValue) map[key.Value.y][key.Value.x] = 'K';
            if (id == 3) map[3][5] = 'T';
            if (id >= 6)
            {
                map[3][5] = '#'; map[4][5] = '#'; map[2][5] = 'T';
            }
            if (id >= 7) { map[3][9] = '#'; map[2][9] = 'T'; }
            if (id == 4) { map[1][2] = '#'; map[1][3] = '#'; map[1][4] = '#'; }
            if (id == 5) { map[1][8] = '#'; map[2][8] = '#'; }
            if (id == 8) map[4][8] = '#';
            if (id == 9) { map[3][3] = '#'; map[4][3] = '#'; }
            if (id == 10) { map[5][12] = '#'; map[2][12] = 'T'; }
            string[] rows = new string[7];
            for (int y = 0; y < 7; y++) rows[y] = new string(map[y]);
            return new ReplayStage(id, name, goal, lesson, intro, hints, rows);
        }

        public static readonly IReadOnlyList<ReplayStage> All = new[]
        {
            Make(1, "地下牢の目覚め", "鍵を拾い、出口から脱出する", "移動と鍵", "冷たい石の床で目を覚ました。鉄格子の向こうに、朝の光がある。",
                new[] { "WASD / 矢印キーで移動できます。", "右下の金色の鍵を拾うと出口の封印が解けます。", "鍵を拾ったら中央の通路へ戻り、右の鉄格子を目指します。" }, exit: new Vector2Int(7, 3), key: new Vector2Int(5, 5)),
            Make(2, "片方の手", "分身に A を踏ませ、扉を通る", "記録と再生", "ひとりでは届かない場所も、過去の自分となら。",
                new[] { "E で記録開始。A の上まで移動します。", "もう一度 E で確定すると、部屋が巻き戻ります。", "分身が A に残る間に、現在の自分で扉を通ります。" }, a: new Vector2Int(3, 5), barriers: 1),
            Make(3, "少し先の未来", "床の罠を避け、分身に遠い A を任せる", "床の罠", "石の隙間から、刃がのぞく。光る予告を見て、進む時を選ぼう。",
                new[] { "床の罠は赤が作動中、金が作動前の予告です。", "記録中に上側の A へ。罠を迂回しても構いません。", "分身に A を任せて進もう。罠は1秒作動・2秒停止です。" }, a: new Vector2Int(5, 1), barriers: 1),
            Make(4, "重さの記憶", "箱を A に押して扉を開ける", "箱", "残せるのは足跡だけじゃない。",
                new[] { "箱の隣から押せます。", "箱を右へ押して A に載せます。", "開いた扉を通って出口へ。" }, a: new Vector2Int(5, 4), barriers: 1, crate: new Vector2Int(4, 4)),
            Make(5, "受け渡し", "分身と箱で二つの扉を開く", "分身と箱", "受け取った時間を、次へ渡そう。",
                new[] { "最初の分身を左側の A に立たせます。", "記録確定後、中央の箱を右へ押し B に載せます。", "二つの扉が開いたら出口へ。" }, a: new Vector2Int(3, 5), b: new Vector2Int(10, 4), barriers: 2, crate: new Vector2Int(9, 4)),
            Make(6, "ふたりの記録", "二体の分身で A と B を同時に踏む", "分身2体", "二度目の自分は、一度目の自分を信じられる。",
                new[] { "一体目を A に記録します。", "二回目の記録では、一体目が再生されます。B へ向かいます。", "二体目を確定したら、現在の自分で出口へ。" }, a: new Vector2Int(3, 5), b: new Vector2Int(9, 5), barriers: 2),
            Make(7, "異なる道", "曲がり角と罠を越え、A と B を分担する", "経路の計画", "短い道が、安全な道とは限らない。記憶を頼りに、進む道を選ぼう。",
                new[] { "A は左上、B は中央上です。", "一体目を A、二体目を B に記録します。", "扉の開く順番を見て進みます。" }, a: new Vector2Int(5, 1), b: new Vector2Int(9, 1), barriers: 2),
            Make(8, "静かな秒針", "待機を使って扉の開く時刻を合わせる", "タイミング", "待つことも、行動のひとつ。",
                new[] { "A は右下、B は中央上です。", "二体目が B に着くまで、扉の手前で待ちます。", "移動キーを離して待つと分身の再生が進みます。" }, a: new Vector2Int(5, 5), b: new Vector2Int(10, 1), barriers: 2),
            Make(9, "箱の向こう", "分身が支える間に箱を使う", "複合", "手を放したあとも、役目は残る。",
                new[] { "A の分身を作り、最初の扉を開けます。", "中央の箱を B に載せます。", "押す向きを確かめてから進みましょう。" }, a: new Vector2Int(4, 1), b: new Vector2Int(10, 4), barriers: 2, crate: new Vector2Int(9, 4)),
            Make(10, "最後の門", "二体の分身と鍵で地上へ脱出する", "総合", "振り返れば、暗闇のなかでも、過去のあなたが灯りをつないでいた。",
                new[] { "一体目を A に、二体目を B に記録します。", "二体目の記録では一体目の再生も進みます。", "二つの扉を抜けたら右上の鍵を回収し、最後の門へ。" }, a: new Vector2Int(4, 5), b: new Vector2Int(9, 1), barriers: 2, key: new Vector2Int(12, 1))
        };
    }
}
