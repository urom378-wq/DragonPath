using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// ボスのHPバー（画面下部に大きく表示）
/// フェーズ表示・エンレイジ演出付き
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("References")]
    public TMP_Text bossNameText;
    public Slider   hpSlider;
    public Image    hpFillImage;
    public Image    delayedFillImage;   // ダメージ遅延表示（残像）
    public TMP_Text hpValueText;
    public GameObject[] phaseIcons;    // フェーズアイコン（3つ）
    public CanvasGroup canvasGroup;

    [Header("Colors")]
    public Color phase1Color  = new Color(0.9f, 0.2f, 0.2f);
    public Color phase2Color  = new Color(1.0f, 0.5f, 0.1f);
    public Color phase3Color  = new Color(1.0f, 0.8f, 0.0f);

    [Header("Animation")]
    public float delayedDecaySpeed = 0.5f;
    public float showHideDuration  = 0.8f;

    private float _targetValue;
    private float _delayedValue;
    private float _delayTimer;
    private float _maxHP;
    private int   _currentPhase = 1;

    // ============================================================
    public void Init(string bossName, float maxHP)
    {
        _maxHP         = maxHP;
        _targetValue   = 1f;
        _delayedValue  = 1f;

        if (bossNameText)   bossNameText.text = bossName;
        if (hpSlider)       { hpSlider.minValue = 0; hpSlider.maxValue = 1; hpSlider.value = 1; }
        if (delayedFillImage) delayedFillImage.fillAmount = 1f;

        UpdatePhaseDisplay(1);
        StartCoroutine(ShowBar());
    }

    public void UpdateHP(float current, float max)
    {
        _targetValue = Mathf.Clamp01(current / max);
        _delayTimer  = 0.6f; // 遅延表示の遅延時間

        if (hpValueText)
            hpValueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

        // フェーズ色変更
        float ratio = current / max;
        int phase = ratio > 0.66f ? 1 : (ratio > 0.33f ? 2 : 3);
        if (phase != _currentPhase)
        {
            _currentPhase = phase;
            UpdatePhaseDisplay(phase);
        }
    }

    public void Hide()
    {
        StartCoroutine(HideBar());
    }

    // ============================================================
    private void Update()
    {
        // メインバー（即時）
        if (hpSlider != null)
            hpSlider.value = Mathf.Lerp(hpSlider.value, _targetValue, Time.deltaTime * 8f);

        // 遅延バー
        if (_delayTimer > 0f)
        {
            _delayTimer -= Time.deltaTime;
        }
        else if (delayedFillImage != null)
        {
            _delayedValue = Mathf.Lerp(_delayedValue, _targetValue, Time.deltaTime * delayedDecaySpeed);
            delayedFillImage.fillAmount = _delayedValue;
        }
    }

    private void UpdatePhaseDisplay(int phase)
    {
        Color targetColor = phase switch
        {
            1 => phase1Color,
            2 => phase2Color,
            3 => phase3Color,
            _ => phase1Color,
        };

        if (hpFillImage) hpFillImage.color = targetColor;

        // フェーズアイコン更新
        for (int i = 0; i < phaseIcons.Length; i++)
        {
            if (phaseIcons[i] != null)
                phaseIcons[i].SetActive(i < phase);
        }

        // フェーズ3はパルスアニメ
        if (phase == 3) StartCoroutine(PulseColor());
    }

    private IEnumerator PulseColor()
    {
        while (_currentPhase == 3 && !_maxHP.Equals(0f))
        {
            if (hpFillImage)
                hpFillImage.color = Color.Lerp(phase3Color, Color.red, Mathf.PingPong(Time.time * 2f, 1f));
            yield return null;
        }
    }

    private IEnumerator ShowBar()
    {
        if (canvasGroup == null) yield break;
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < showHideDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = t / showHideDuration;
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    private IEnumerator HideBar()
    {
        if (canvasGroup == null) yield break;
        float t = 0f;
        float start = canvasGroup.alpha;
        while (t < showHideDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 0f, t / showHideDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
