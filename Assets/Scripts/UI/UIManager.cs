using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 全UI画面の一元管理（HUD・ポーズ・インベントリ・ゲームオーバー・クリア）
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public enum NotificationType { Info, Item, Warning, Quest, LevelUp }

    [Header("Panels")]
    public CanvasGroup hudPanel;
    public CanvasGroup pausePanel;
    public CanvasGroup inventoryPanel;
    public CanvasGroup gameOverPanel;
    public CanvasGroup victoryPanel;

    [Header("Pause Panel")]
    public Button pauseResumeBtn;
    public Button pauseRestartBtn;
    public Button pauseQuitBtn;
    public Slider pauseBGMSlider;
    public Slider pauseSESlider;

    [Header("Game Over Panel")]
    public Button gameOverRestartBtn;
    public Button gameOverQuitBtn;
    public TMP_Text gameOverStatsText;

    [Header("Victory Panel")]
    public Button victoryRestartBtn;
    public Button victoryQuitBtn;
    public TMP_Text victoryStatsText;

    [Header("Notification")]
    public TMP_Text notificationText;
    public CanvasGroup notificationGroup;

    [Header("Screen Effects")]
    public Image damageVignette;          // ダメージ時の赤縁取り
    public CanvasGroup fadePanel;         // フェードイン/アウト

    [Header("Inventory UI")]
    public InventoryUI inventoryUI;

    // ============================================================
    private Coroutine _notifCoroutine;
    private Coroutine _shakeCoroutine;
    private Vector3   _camOriginalPos;
    private Camera    _cam;

    // ============================================================
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _cam = Camera.main;
        if (_cam != null) _camOriginalPos = _cam.transform.localPosition;

        SetupButtons();
        SetupSliders();

        GameManager.OnStateChanged += OnGameStateChanged;
        var playerStats = FindFirstObjectByType<PlayerStats>();
        if (playerStats != null)
            playerStats.OnDamageReceived += ShowDamageEffect;

        // 初期フェードイン
        StartCoroutine(FadeIn(1f));
    }

    private void OnDestroy()
    {
        GameManager.OnStateChanged -= OnGameStateChanged;
    }

    // ============================================================
    // State Change Handler
    // ============================================================
    private void OnGameStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.Playing:
                ShowPanel(hudPanel, true);
                ShowPanel(pausePanel, false);
                ShowPanel(inventoryPanel, false);
                break;

            case GameManager.GameState.Paused:
                ShowPanel(pausePanel, true);
                UpdatePauseStats();
                break;

            case GameManager.GameState.Inventory:
                ShowPanel(inventoryPanel, true);
                inventoryUI?.Refresh();
                break;

            case GameManager.GameState.GameOver:
                StartCoroutine(ShowGameOver());
                break;

            case GameManager.GameState.Victory:
                StartCoroutine(ShowVictory());
                break;
        }
    }

    private void ShowPanel(CanvasGroup panel, bool show)
    {
        if (panel == null) return;
        panel.alpha          = show ? 1f : 0f;
        panel.interactable   = show;
        panel.blocksRaycasts = show;
    }

    // ============================================================
    // Panel Setup
    // ============================================================
    private void SetupButtons()
    {
        pauseResumeBtn ?.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); GameManager.Instance?.SetState(GameManager.GameState.Playing); });
        pauseRestartBtn?.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); GameManager.Instance?.RestartGame(); });
        pauseQuitBtn   ?.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); GameManager.Instance?.QuitGame(); });

        gameOverRestartBtn?.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); GameManager.Instance?.RestartGame(); });
        gameOverQuitBtn   ?.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); GameManager.Instance?.QuitGame(); });

        victoryRestartBtn?.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); GameManager.Instance?.RestartGame(); });
        victoryQuitBtn   ?.onClick.AddListener(() => { AudioManager.Instance?.PlayUIClick(); GameManager.Instance?.QuitGame(); });
    }

    private void SetupSliders()
    {
        if (pauseBGMSlider != null)
        {
            pauseBGMSlider.value = AudioManager.Instance?.bgmVolume ?? 0.6f;
            pauseBGMSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetBGMVolume(v));
        }
        if (pauseSESlider != null)
        {
            pauseSESlider.value = AudioManager.Instance?.seVolume ?? 1f;
            pauseSESlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSEVolume(v));
        }
    }

    // ============================================================
    // Game Over / Victory
    // ============================================================
    private IEnumerator ShowGameOver()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        yield return StartCoroutine(FadeOut(0.8f));

        var stats = FindFirstObjectByType<PlayerStats>();
        if (gameOverStatsText != null && stats != null)
        {
            gameOverStatsText.text =
                $"到達レベル: {stats.Level}\n" +
                $"敵討伐数: {GameManager.Instance?.EnemyKillCount}\n" +
                $"プレイ時間: {SaveSystem.Instance?.GetFormattedPlayTime() ?? "--"}";
        }

        ShowPanel(gameOverPanel, true);
        yield return StartCoroutine(FadeIn(0.8f));
    }

    private IEnumerator ShowVictory()
    {
        yield return new WaitForSecondsRealtime(2f);
        yield return StartCoroutine(FadeOut(1f));

        var stats = FindFirstObjectByType<PlayerStats>();
        if (victoryStatsText != null && stats != null)
        {
            victoryStatsText.text =
                $"レベル: {stats.Level}\n" +
                $"討伐数: {GameManager.Instance?.EnemyKillCount}\n" +
                $"プレイ時間: {SaveSystem.Instance?.GetFormattedPlayTime() ?? "--"}";
        }

        ShowPanel(victoryPanel, true);
        AudioManager.Instance?.PlayBGM(AudioManager.Instance.bgmVictory, true);
        yield return StartCoroutine(FadeIn(1f));
    }

    private void UpdatePauseStats() { /* スロット選択など拡張可 */ }

    // ============================================================
    // Notification (トースト通知)
    // ============================================================
    public void ShowNotification(string message, NotificationType type = NotificationType.Info)
    {
        if (_notifCoroutine != null) StopCoroutine(_notifCoroutine);
        _notifCoroutine = StartCoroutine(NotificationRoutine(message, type));
    }

    private IEnumerator NotificationRoutine(string message, NotificationType type)
    {
        if (notificationText  == null || notificationGroup == null) yield break;

        notificationText.text = message;
        notificationText.color = type switch
        {
            NotificationType.Item    => Color.cyan,
            NotificationType.Warning => Color.red,
            NotificationType.Quest   => Color.yellow,
            NotificationType.LevelUp => new Color(1f, 0.8f, 0f),
            _                        => Color.white,
        };

        notificationGroup.alpha = 0f;
        float t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; notificationGroup.alpha = t / 0.3f; yield return null; }
        notificationGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(2f);

        t = 0f;
        while (t < 0.4f) { t += Time.unscaledDeltaTime; notificationGroup.alpha = 1f - t / 0.4f; yield return null; }
        notificationGroup.alpha = 0f;
    }

    // ============================================================
    // Damage Vignette
    // ============================================================
    private void ShowDamageEffect(float damage)
    {
        if (_damageCoroutine != null) StopCoroutine(_damageCoroutine);
        _damageCoroutine = StartCoroutine(DamageVignetteRoutine());
    }

    private Coroutine _damageCoroutine;
    private IEnumerator DamageVignetteRoutine()
    {
        if (damageVignette == null) yield break;
        Color c = damageVignette.color;
        c.a = 0.6f;
        damageVignette.color = c;

        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0.6f, 0f, t / 0.5f);
            damageVignette.color = c;
            yield return null;
        }
        c.a = 0f;
        damageVignette.color = c;
    }

    // ============================================================
    // Screen Shake
    // ============================================================
    public void TriggerScreenShake(float intensity, float duration)
    {
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(ScreenShakeRoutine(intensity, duration));
    }

    private IEnumerator ScreenShakeRoutine(float intensity, float duration)
    {
        if (_cam == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;
            _cam.transform.localPosition = _camOriginalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            intensity = Mathf.Lerp(intensity, 0f, elapsed / duration);
            yield return null;
        }
        _cam.transform.localPosition = _camOriginalPos;
    }

    // ============================================================
    // Fade
    // ============================================================
    public IEnumerator FadeIn(float duration)
    {
        if (fadePanel == null) yield break;
        fadePanel.gameObject.SetActive(true);
        fadePanel.alpha = 1f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadePanel.alpha = 1f - t / duration;
            yield return null;
        }
        fadePanel.alpha = 0f;
        fadePanel.gameObject.SetActive(false);
    }

    public IEnumerator FadeOut(float duration)
    {
        if (fadePanel == null) yield break;
        fadePanel.gameObject.SetActive(true);
        fadePanel.alpha = 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadePanel.alpha = t / duration;
            yield return null;
        }
        fadePanel.alpha = 1f;
    }
}
