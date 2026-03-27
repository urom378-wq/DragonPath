using UnityEngine;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// ゲーム全体の状態管理シングルトン
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Paused, Inventory, Dialogue, GameOver, Victory }

    private GameState _state = GameState.Playing;
    public GameState State => _state;
    public bool IsPlaying => _state == GameState.Playing;

    // === イベント ===
    public static event Action<GameState> OnStateChanged;
    public static event Action           OnPlayerDied;
    public static event Action           OnBossDefeated;
    public static event Action<int>      OnEnemyKilled;   // killCount

    private int _enemyKillCount = 0;
    public int EnemyKillCount => _enemyKillCount;

    [Header("Game Settings")]
    public int targetFrameRate = 60;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount  = 0;
    }

    private void Start()
    {
        SetState(GameState.Playing);
    }

    // ============================================================
    // State Machine
    // ============================================================
    public void SetState(GameState newState)
    {
        if (_state == newState) return;
        _state = newState;

        switch (newState)
        {
            case GameState.Playing:
                Time.timeScale           = 1f;
                Cursor.lockState         = CursorLockMode.Locked;
                Cursor.visible           = false;
                break;

            case GameState.Paused:
            case GameState.Inventory:
            case GameState.Dialogue:
                Time.timeScale           = 0f;
                Cursor.lockState         = CursorLockMode.None;
                Cursor.visible           = true;
                break;

            case GameState.GameOver:
                Time.timeScale           = 0f;
                Cursor.lockState         = CursorLockMode.None;
                Cursor.visible           = true;
                OnPlayerDied?.Invoke();
                break;

            case GameState.Victory:
                Time.timeScale           = 0.4f;
                Cursor.lockState         = CursorLockMode.None;
                Cursor.visible           = true;
                OnBossDefeated?.Invoke();
                break;
        }

        OnStateChanged?.Invoke(newState);
    }

    public void TogglePause()
    {
        if (_state == GameState.Playing)  SetState(GameState.Paused);
        else if (_state == GameState.Paused) SetState(GameState.Playing);
    }

    public void ToggleInventory()
    {
        if (_state == GameState.Playing)   SetState(GameState.Inventory);
        else if (_state == GameState.Inventory) SetState(GameState.Playing);
    }

    // ============================================================
    // Game Events
    // ============================================================
    public void NotifyEnemyKilled()
    {
        _enemyKillCount++;
        OnEnemyKilled?.Invoke(_enemyKillCount);
    }

    public void TriggerGameOver()  => SetState(GameState.GameOver);
    public void TriggerVictory()   => SetState(GameState.Victory);

    // ============================================================
    // Scene Management
    // ============================================================
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadScene(int index)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(index);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
