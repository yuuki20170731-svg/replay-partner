using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace ReplayPartner.Editor {
public static class ReplaySmokeTests {
static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
static Vector2Int Find(ReplaySimulation s,char c) {
for(int y=0;y<s.Stage.Map.Length;y++) for(int x=0;x<s.Stage.Map[y].Length;x++) if(s.Stage.Map[y][x]==c) return new Vector2Int(x,y);
return new Vector2Int(-1,-1);
}
static void Wait(ReplaySimulation s) {
int n=0; for(int i=0;i<s.RecordingsCount;i++) n=Mathf.Max(n,s.RecordingLength(i));
while(s.Tick<n) s.Advance(Vector2.zero);
}
static void Walk(ReplaySimulation s,Vector2Int goal) {
var start=ReplaySimulation.TileAt(s.Player);
var q=new Queue<Vector2Int>(); var parents=new Dictionary<Vector2Int,Vector2Int>();
q.Enqueue(start); parents[start]=start;
var dirs=new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
while(q.Count>0&&!parents.ContainsKey(goal)) {
var p=q.Dequeue();
foreach(var d in dirs) {
var n=p+d; char c=s.Cell(n); bool box=false;
foreach(var b in s.Crates) box|=b==n;
if(parents.ContainsKey(n)||c=='#'||c=='T'||box||((c=='a'||c=='b')&&!s.IsOpen(c))||(c=='E'&&s.NeedsKey&&!s.HasKey)) continue;
parents[n]=p; q.Enqueue(n);
}}
Check(parents.ContainsKey(goal),"No safe route "+s.Stage.Id+" to "+goal);
var route=new List<Vector2Int>();
for(var p=goal;p!=start;p=parents[p]) route.Add(p);
route.Reverse();
foreach(var p in route) {
var d=p-ReplaySimulation.TileAt(s.Player);
for(int i=0;i<12;i++) s.Advance(d);
Check(s.Mode==ReplayMode.Cleared||Vector2.Distance(s.Player,p)<.01f,"Blocked "+s.Stage.Id+" at "+p+" actual "+s.Player);
}}
static void Record(ReplaySimulation s,char c) {
Check(s.Begin(),"Begin"); Wait(s); Walk(s,Find(s,c)); Check(s.Commit(),"Commit");
}
[MenuItem("Replay Partner/Run Smoke Tests")]
public static void Run() {
Check(ReplayStage.All.Count==10,"Ten stages");
foreach(var stage in ReplayStage.All) {
var s=new ReplaySimulation(stage);
if(Find(s,'A').x>=0&&stage.Id!=4) Record(s,'A');
if(Find(s,'B').x>=0&&s.Crates.Count==0) Record(s,'B');
Wait(s);
if(s.Crates.Count>0) {
var box=s.Crates[0]; Walk(s,box+Vector2Int.left); s.Step(ReplayCommand.Right);
Check(s.Crates[0]==box+Vector2Int.right,"Crate "+stage.Id);
}
if(s.NeedsKey) Walk(s,Find(s,'K'));
Walk(s,Find(s,'E')); Check(s.Mode==ReplayMode.Cleared,"Clear "+stage.Id);
}
var f=new ReplaySimulation(ReplayStage.All[1]); var start=f.Player;
f.Advance(Vector2.right); Check(f.Player.x>start.x&&f.Player.x<start.x+1,"Continuous");
f.Restart(); f.Advance(Vector2.one);
Check(Mathf.Abs(Vector2.Distance(start,f.Player)-ReplaySimulation.WalkSpeed/60f)<.0001f,"Diagonal");
f.Restart(); f.Begin(); for(int i=0;i<7;i++) f.Advance(Vector2.right);
var end=f.Player; f.Commit(); for(int i=0;i<7;i++) f.Advance(Vector2.zero);
Check(f.RecordedPath(0).Count==8&&Vector2.Distance(f.RecordedPath(0)[7],end)<.0001f,"Recorded trail retains exact fractional endpoint");
Check(Vector2.Distance(f.Clones[0].Position,end)<.0001f,"Fractional replay");
f.Retry(); Check(f.RecordingsCount==1&&f.Health==3,"Retry preserves recordings");
f.Begin(); f.Advance(Vector2.right); f.Cancel();
Check(f.Mode==ReplayMode.Playing&&f.RecordingsCount==1&&f.Remaining==ReplaySimulation.RecordingLimit&&f.Tick==0,"Cancel only current recording");
Check(f.RecordedPath(0).Count==8,"Cancel preserves existing trail");
f.Restart(); f.Begin(); for(int i=0;i<ReplaySimulation.RecordingLimit;i++) f.Advance(Vector2.zero);
Check(f.Mode==ReplayMode.Playing&&f.RecordingsCount==1,"Limit");
var h=new ReplaySimulation(new ReplayStage(0,"trap","","","",Array.Empty<string>(),new[]{"########","#PTTTTT#","########"}));
for(int i=0;i<600&&h.Mode!=ReplayMode.Failed;i++) h.Advance(Vector2.right);
Check(h.Mode==ReplayMode.Failed&&h.Health==0,"Three trap hits");
h.Retry(); Check(h.Health==3&&h.Tick==0,"Trap reset");
Check(h.HasTraps&&Mathf.Abs(h.TrapCountdown-1f)<.001f,"Trap initial countdown");
for(int i=0;i<60;i++) h.Advance(Vector2.zero);
Check(!h.TrapsActive&&Mathf.Abs(h.TrapCountdown-2f)<.001f,"Trap safe interval");
for(int i=0;i<90;i++) h.Advance(Vector2.zero);
Check(h.TrapsWarning&&Mathf.Abs(h.TrapCountdown-.5f)<.001f,"Trap advance warning");
f.Undo(); Check(f.RecordingsCount==0,"Undo removes recording and trail");
var puppet=new DungeonPuppet(Color.green);
puppet.Animate(1f,true,1f,false); puppet.Animate(0f,false,-1f,true);
Check(puppet.Root.Q<Image>("explorer-sprite")?.sprite!=null,"Painted explorer sprite loaded");
var idleSprite=puppet.Root.Q<Image>("explorer-sprite").sprite;
puppet.Animate(1f,true,1f,false);
Check(puppet.Root.Q<Image>("explorer-sprite").sprite!=idleSprite,"Walking changes painted frame");
puppet.Animate(0f,false,1f,false,true);
Check(puppet.Root.Q<Image>("explorer-sprite").sprite!=idleSprite,"Rear-facing idle uses back artwork");
var rearIdle=puppet.Root.Q<Image>("explorer-sprite").sprite;
puppet.Animate(1f,true,1f,false,true);
Check(puppet.Root.Q<Image>("explorer-sprite").sprite!=rearIdle,"Rear-facing walking changes frame");
var keys=ReplayControls.Defaults;
Check(ReplayControls.Valid(keys),"Default bindings valid");
Check(!ReplayControls.CanAssign(keys,0,KeyCode.DownArrow),"Fixed arrow must not cancel opposite movement");
Check(!ReplayControls.CanAssign(keys,4,KeyCode.Space),"UI activation key reserved");
Check(!ReplayControls.CanAssign(keys,4,KeyCode.R),"Duplicate binding rejected");
keys[0]=KeyCode.S; keys[1]=KeyCode.W;
Check(ReplayControls.Valid(keys),"Valid swapped settings load together");
keys[2]=KeyCode.S; Check(!ReplayControls.Valid(keys),"Corrupt duplicate settings detected");
Debug.Log("Replay Partner smoke tests passed: 10 stages, continuous and diagonal movement, fractional replay, crates, keys, traps, retry, recording limit.");
}
}}
