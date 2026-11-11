using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BalHanuman
{
    /// <summary>Scene-level hub: counters, checkpoints, respawn, pause, dialogue helper and the ending sequence.</summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }

        [Header("Scene references")]
        public PlayerController2D player;
        public PlayerInteractor interactor;
        public QuestManager quest;
        public SideScrollCamera cameraRig;
        public HudController hud;
        public OverlayUI overlay;
        public GameObject carriedBasket;

        [Header("Config")]
        public Vector2 sageReturnPosition = new Vector2(10f, 0.1f);
        public string mainMenuScene = "MainMenu";
        public float fallLimitY = -9f;
        public Sprite sparkleSprite;
        public Sprite goldSprite;
        [Tooltip("Show the 'Level 1' title card when the level opens")]
        public bool showTitleCard = true;

        public int Mangoes { get; private set; }
        public int TotalMangoes { get; private set; }
        public int Bonus { get; private set; }
        public bool MonkeyRescued { get; set; }
        public bool IsPaused { get; private set; }
        public event Action LevelCompleted;

        Vector2 checkpoint;
        float elapsed;
        bool ending, respawning, failOpen;

        void Awake()
        {
            Current = this;
            Time.timeScale = 1f;
            GameInput.Reset();
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
        }

        void Start()
        {
            checkpoint = player.transform.position;
            quest.StateChanged += OnQuestChanged;
            interactor.TargetChanged += t => hud.SetPrompt(t != null ? t.GetInteractionPrompt() : null);
            hud.SetObjective(quest.Objective, quest.Hint);
            TotalMangoes = 0;
            foreach (var col in FindObjectsByType<Collectible>(FindObjectsSortMode.None)) if (col.kind == CollectibleKind.Mango) TotalMangoes += col.value;
            foreach (var m in FindObjectsByType<BabyMonkey>(FindObjectsSortMode.None)) TotalMangoes += m.bonusMangoes;
            hud.SetMangoCount(0, TotalMangoes);
            player.RollDenied += hud.FlashEnergy;
            player.RollDenied += Haptics.Tick;
            if (showTitleCard) hud.ShowTitleCard("LEVEL 1", "The Missing Fruit Offering");
            if (carriedBasket != null) carriedBasket.SetActive(false);
        }

        void OnDestroy() { if (Current == this) Current = null; }

        void OnQuestChanged(QuestState s)
        {
            hud.SetObjective(quest.Objective, quest.Hint);
            if (carriedBasket != null) carriedBasket.SetActive(s == QuestState.BasketCollected || s == QuestState.ReturnToSage);
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) TogglePause();

            if (!IsPaused && Time.timeScale > 0f && quest.State != QuestState.NotStarted && quest.State != QuestState.Completed)
                elapsed += Time.deltaTime;

            hud.ShowClimbButtons(player.InVine && !ending);

            if (!respawning && !ending && player.transform.position.y < fallLimitY) Respawn();
        }

        // ---------------- helpers used by world objects ----------------
        public void Toast(string text, float seconds = 3f) { hud.ShowToast(text, seconds); }

        public void Dialogue(DialogueLine[] lines, Action done = null)
        {
            Time.timeScale = 0f;
            overlay.ShowDialogue(lines, () => { Time.timeScale = 1f; done?.Invoke(); });
        }

        public void Collect(CollectibleKind kind, int value)
        {
            if (kind == CollectibleKind.Mango) AddMangoes(value);
            else Bonus += value;
        }

        public void AddMangoes(int n) { Mangoes += n; hud.SetMangoCount(Mangoes, TotalMangoes); }

        public void SetCheckpoint(Vector2 pos) { checkpoint = pos; }

        // ---------------- respawn ----------------
        /// <summary>The player fell into water or a pit: show the "Hanuman is down" dialog instead of respawning at once.</summary>
        public void Respawn()
        {
            if (respawning || ending || failOpen) return;
            Haptics.Heavy();
            SideScrollCamera.Shake(0.12f, 0.3f);
            StartCoroutine(FailRoutine());
        }

        IEnumerator FailRoutine()
        {
            failOpen = true;
            player.InputLocked = true;
            GameInput.Reset();
            hud.SetControlsVisible(false);
            yield return new WaitForSeconds(0.5f);          // let the splash play
            player.spriteRenderer.enabled = false;           // the dialog shows Hanuman's fall sprites instead
            player.Body.simulated = false;
            Time.timeScale = 0f;
            overlay.ShowFail(GoToCheckpoint, ReplayLevel);
        }

        public void GoToCheckpoint()
        {
            overlay.HideFail();
            Time.timeScale = 1f;
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            respawning = true;
            player.InputLocked = true;
            yield return hud.FadeTo(1f, 0.25f);
            player.Body.simulated = true;
            player.spriteRenderer.enabled = true;
            player.TeleportTo(checkpoint);
            if (cameraRig != null) cameraRig.Snap();
            hud.SetControlsVisible(true);
            yield return new WaitForSecondsRealtime(0.1f);
            yield return hud.FadeTo(0f, 0.3f);
            player.InputLocked = false;
            respawning = false; failOpen = false;
        }

        // ---------------- return portal ----------------
        public void TravelThroughPortal()
        {
            StartCoroutine(PortalRoutine());
        }

        IEnumerator PortalRoutine()
        {
            player.InputLocked = true;
            Sfx.Play(Sfx.Id.Portal);
            yield return hud.FadeTo(1f, 0.5f);
            player.TeleportTo(sageReturnPosition);
            checkpoint = sageReturnPosition;
            if (cameraRig != null) cameraRig.Snap();
            quest.ReachSage();
            yield return new WaitForSecondsRealtime(0.2f);
            yield return hud.FadeTo(0f, 0.5f);
            player.InputLocked = false;
            Toast("You're back! Give the basket to the sage.", 4f);
        }

        // ---------------- pause ----------------
        public void TogglePause()
        {
            if (ending || failOpen || overlay.DialogueOpen) return;
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
            overlay.SetPause(IsPaused);
            GameInput.Reset();
            Sfx.Play(Sfx.Id.Click);
        }

        public void ReplayLevel()
        {
            Time.timeScale = 1f; GameInput.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f; GameInput.Reset();
            SceneManager.LoadScene(mainMenuScene);
        }

        // ---------------- ending ----------------
        public void BeginEnding(Vector3 sagePos)
        {
            if (ending || !quest.Complete()) return;
            ending = true;
            StartCoroutine(EndingRoutine(sagePos));
        }

        IEnumerator EndingRoutine(Vector3 sagePos)
        {
            player.InputLocked = true;
            hud.SetPrompt(null);
            hud.SetControlsVisible(false);
            yield return new WaitForSeconds(0.3f);
            Music.Duck(0.25f, 9f);
            Sfx.Play(Sfx.Id.Bell);
            Toast("The temple bell rings across the forest...", 3.5f);
            Fx.Burst(sparkleSprite, player.transform.position + Vector3.up, 40, 4f, 2.2f, -0.2f, 0.45f, new Color(1f, 0.85f, 0.3f), 1.5f, 30);
            Fx.Burst(goldSprite, sagePos + Vector3.up, 14, 3f, 2f, 1.5f, 0.4f, Color.white, 3f, 30);
            foreach (var a in FindObjectsByType<AmbientAnimal>(FindObjectsSortMode.None)) a.Celebrate();
            yield return new WaitForSeconds(1.6f);
            Fx.Burst(sparkleSprite, player.transform.position + Vector3.up * 2f, 40, 5f, 2.5f, 0.5f, 0.4f, new Color(1f, 0.9f, 0.5f), 2f, 30);
            Sfx.Play(Sfx.Id.Complete);
            yield return new WaitForSeconds(2.2f);
            LevelCompleted?.Invoke();
            overlay.ShowComplete(new CompletionStats { mangoes = Mangoes, totalMangoes = TotalMangoes, bonus = Bonus, monkeyRescued = MonkeyRescued, seconds = elapsed });
        }
    }
}
