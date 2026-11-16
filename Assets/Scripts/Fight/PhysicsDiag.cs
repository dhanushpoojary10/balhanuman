#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using B = BalHanuman.Fight.FightInput.Btn;

namespace BalHanuman.Fight
{
    /// <summary>Development tool: spams the controls the way a player would and records physics, frame pacing, camera motion and the boss pictures to Temp/diag.txt.</summary>
    public class PhysicsDiag : MonoBehaviour
    {
        public static void Run() { new GameObject("PhysicsDiag").AddComponent<PhysicsDiag>(); }

        FightDirector d; HanumanFighter h; AhiravanaFighter b; Camera cam;
        readonly StringBuilder rep = new StringBuilder();
        void Log(string s) { rep.AppendLine(s); File.WriteAllText("Temp/diag.txt", rep.ToString()); }

        IEnumerator Fresh(float dist)
        {
            FightInput.Reset(); d.Restart(FightSetup.Mode.Training);
            yield return new WaitForSecondsRealtime(1.6f);
            d = FightDirector.I; h = d.hanuman; b = d.boss; cam = Object.FindAnyObjectByType<FightCamera>().GetComponent<Camera>();
            h.infiniteMeter = true; for (int i = 0; i < h.unlocked.Length; i++) h.unlocked[i] = true;
            b.dummy = AhiravanaFighter.Dummy.Idle; b.maxHp = b.hp = 100000f; h.maxHp = h.hp = 100000f;
            h.transform.position = new Vector3(-2f, 0f, 0f); b.transform.position = new Vector3(-2f + dist, 0f, 0f);
            h.facing = 1; b.facing = -1; h.vel = b.vel = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.4f);
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            d = FightDirector.I; Time.timeScale = 1f;
            Log("PHYSICS / FEEL DIAG " + System.DateTime.Now);

            // 1. kick spam on the ground
            yield return StartCoroutine(Measure("1. kick spam x12 (0.2s apart), boss 1.4m away", 1.4f, 4.5f, Spam(B.Kick, 12, 0.2f, false)));
            // 2. gada spam on the ground
            yield return StartCoroutine(Measure("2. gada spam x12 (0.2s apart)", 1.4f, 4.5f, Spam(B.Mace, 12, 0.2f, false)));
            // 3. jump, then gada spam in the air
            yield return StartCoroutine(Measure("3. jump then gada spam x10 (0.2s apart)", 3.0f, 4.0f, JumpSpam(B.Mace, 10, 0.2f)));
            // 4. jump, then kick spam in the air
            yield return StartCoroutine(Measure("4. jump then kick spam x10", 3.0f, 3.5f, JumpSpam(B.Kick, 10, 0.2f)));
            // 5. jump button mashing
            yield return StartCoroutine(Measure("5. jump button mashed x8", 3.0f, 3.5f, Spam(B.Jump, 8, 0.25f, false)));
            // 6. walk, nothing else (frame pacing baseline)
            yield return StartCoroutine(Measure("6. walk forward 2s (baseline pacing)", 6.0f, 2.2f, Walk()));
            // 7. launcher juggle then air gada
            yield return StartCoroutine(Measure("7. launcher then gada spam", 1.4f, 4.5f, LauncherJuggle()));
            Log(""); Log("DIAG DONE");
            FightInput.Reset(); Destroy(gameObject);
        }

        IEnumerator Spam(B btn, int n, float gap, bool dummy)
        {
            for (int i = 0; i < n; i++)
            {
                FightInput.TouchDown(btn); yield return new WaitForSecondsRealtime(0.05f); FightInput.TouchUp(btn);
                yield return new WaitForSecondsRealtime(gap);
            }
        }

        IEnumerator JumpSpam(B btn, int n, float gap)
        {
            FightInput.TouchDown(B.Jump); yield return new WaitForSecondsRealtime(0.05f); FightInput.TouchUp(B.Jump);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return StartCoroutine(Spam(btn, n, gap, false));
        }

        IEnumerator Walk() { FightInput.Stick = new Vector2(1, 0); yield return new WaitForSecondsRealtime(2.0f); FightInput.Stick = Vector2.zero; }

        IEnumerator LauncherJuggle()
        {
            FightInput.Stick = new Vector2(0, 1);
            FightInput.TouchDown(B.Mace); yield return new WaitForSecondsRealtime(0.06f); FightInput.TouchUp(B.Mace);
            yield return new WaitForSecondsRealtime(0.15f); FightInput.Stick = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return StartCoroutine(Spam(B.Mace, 10, 0.2f, false));
        }

        IEnumerator Measure(string name, float dist, float dur, IEnumerator action)
        {
            yield return StartCoroutine(Fresh(dist));
            StartCoroutine(action);
            float t0 = Time.unscaledTime;
            float maxHy = 0f, maxHvy = 0f, maxBy = 0f, airHanuman = 0f, airBoss = 0f, frozen = 0f;
            float maxDt = 0f, sumDt = 0f; int frames = 0, over40 = 0;
            Vector3 lastCam = cam.transform.position; float lastSize = cam.orthographicSize, lastRoll = 0f;
            double camMove = 0, camSize = 0, camRoll = 0; float camMaxMove = 0f;
            int launchEpisodes = 0, tumbleRestarts = 0, bossPoseChanges = 0; string lastBossState = null, lastPose = null; int launchedFrames = 0;
            string lastHState = null; int hStateChanges = 0; float lastHy = 0f, hyRise = 0f; float lastX = h.transform.position.x; float flipCount = 0; int lastFacing = h.facing;
            float maxFlipPerSec = 0f;
            var poseSeq = new StringBuilder(); string lastTumble = null;
            while (Time.unscaledTime - t0 < dur)
            {
                float dt = Time.unscaledDeltaTime; frames++; sumDt += dt; maxDt = Mathf.Max(maxDt, dt); if (dt > 0.04f) over40++;
                float hy = h.transform.position.y, by = b.transform.position.y;
                maxHy = Mathf.Max(maxHy, hy); maxHvy = Mathf.Max(maxHvy, h.vel.y); maxBy = Mathf.Max(maxBy, by);
                if (!h.grounded) airHanuman += dt; if (!b.grounded) airBoss += dt;
                if (hy > lastHy) hyRise += hy - lastHy; lastHy = hy;
                if (h.hitstop > 0f) frozen += dt;
                var cp = cam.transform.position; var mv = (cp - lastCam).magnitude; camMove += mv * mv; camMaxMove = Mathf.Max(camMaxMove, mv); lastCam = cp;
                camSize += Mathf.Pow(cam.orthographicSize - lastSize, 2); lastSize = cam.orthographicSize;
                float roll = cam.transform.eulerAngles.z; if (roll > 180f) roll -= 360f; camRoll += Mathf.Pow(roll - lastRoll, 2); lastRoll = roll;
                string bs = b.state.ToString();
                if (bs != lastBossState) { if (bs == "Launched") launchEpisodes++; lastBossState = bs; }
                if (b.state == FState.Launched) launchedFrames++;
                string pose = b.puppet.poseSprite != null && b.puppet.poseSprite.sprite != null ? b.puppet.poseSprite.sprite.name : "rig";
                if (pose != lastPose)
                {
                    bossPoseChanges++;
                    if (pose.StartsWith("s48")) { if (lastTumble != null && pose == "s48_00") tumbleRestarts++; lastTumble = pose; poseSeq.Append(pose.Substring(4) + " "); }
                    lastPose = pose;
                }
                string hs = h.Attacking ? h.move.id : h.state.ToString();
                if (hs != lastHState) { hStateChanges++; lastHState = hs; }
                if (h.facing != lastFacing) { flipCount++; lastFacing = h.facing; }
                yield return null;
            }
            float n = Mathf.Max(1, frames);
            Log("");
            Log("-- " + name);
            Log("   frames=" + frames + " avgFrame=" + (sumDt / n * 1000f).ToString("0.0") + "ms maxFrame=" + (maxDt * 1000f).ToString("0") + "ms framesOver40ms=" + over40);
            Log("   hanuman: maxHeight=" + maxHy.ToString("0.0") + "m maxUpSpeed=" + maxHvy.ToString("0.0") + " airborne=" + airHanuman.ToString("0.0") + "s totalClimb=" + hyRise.ToString("0.0") + "m facingFlips=" + flipCount + " stateChanges=" + hStateChanges + " frozenByHitstop=" + frozen.ToString("0.00") + "s");
            Log("   boss: maxHeight=" + maxBy.ToString("0.0") + "m airborne=" + airBoss.ToString("0.0") + "s launchEpisodes=" + launchEpisodes + " framesInLaunched=" + launchedFrames + " tumblePictureRestarts=" + tumbleRestarts + " tumbleSeq=[" + poseSeq + "]");
            Log("   camera: rmsMove=" + Mathf.Sqrt((float)(camMove / n)).ToString("0.000") + "u/frame maxMove=" + camMaxMove.ToString("0.00") + " rmsSizeChange=" + Mathf.Sqrt((float)(camSize / n)).ToString("0.000") + " rmsRollChange=" + Mathf.Sqrt((float)(camRoll / n)).ToString("0.000") + "deg/frame");
            FightInput.Reset();
        }
    }
}
#endif
