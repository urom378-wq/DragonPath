using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;

/// <summary>
/// 会話UIシステム - タイプライター演出・選択肢・ポートレート表示
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("Panel")]
    public CanvasGroup dialoguePanel;
    public float       showDuration = 0.3f;

    [Header("Content")]
    public Image      portrait;
    public TMP_Text   nameText;
    public TMP_Text   dialogueText;

    [Header("Typewriter")]
    public float typewriterSpeed = 0.04f;   // 秒/文字

    [Header("Next Indicator")]
    public GameObject nextIndicator;   // 「→」アイコンなど

    private string[]  _lines;
    private int       _lineIndex = 0;
    private bool      _isTyping  = false;
    private bool      _isActive  = false;

    private Coroutine _typeCoroutine;

    // ============================================================
    private void Start()
    {
        if (dialoguePanel != null) { dialoguePanel.alpha = 0f; dialoguePanel.interactable = false; }
        if (nextIndicator != null)  nextIndicator.SetActive(false);
    }

    private void Update()
    {
        if (!_isActive) return;

        bool advance = Keyboard.current?.fKey.wasPressedThisFrame == true ||
                       Keyboard.current?.spaceKey.wasPressedThisFrame == true ||
                       Keyboard.current?.returnKey.wasPressedThisFrame == true;

        if (!advance) return;

        if (_isTyping)
        {
            // タイプ中はスキップ
            SkipTypewriter();
        }
        else
        {
            NextLine();
        }
    }

    // ============================================================
    public void StartDialogue(string speakerName, Sprite portrait, string[] lines)
    {
        if (lines == null || lines.Length == 0) return;

        _lines      = lines;
        _lineIndex  = 0;
        _isActive   = true;

        if (nameText != null)   nameText.text = speakerName;
        if (this.portrait != null) this.portrait.sprite = portrait;

        StartCoroutine(ShowPanel());
        ShowLine(_lines[_lineIndex]);
    }

    private void NextLine()
    {
        _lineIndex++;
        if (_lineIndex >= _lines.Length)
        {
            EndDialogue();
            return;
        }
        ShowLine(_lines[_lineIndex]);
    }

    private void ShowLine(string line)
    {
        if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
        _typeCoroutine = StartCoroutine(TypewriterRoutine(line));
        if (nextIndicator != null) nextIndicator.SetActive(false);
    }

    private IEnumerator TypewriterRoutine(string line)
    {
        _isTyping = true;
        if (dialogueText != null) dialogueText.text = "";

        foreach (char c in line)
        {
            if (dialogueText != null) dialogueText.text += c;
            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }

        _isTyping = false;
        if (nextIndicator != null) nextIndicator.SetActive(true);
    }

    private void SkipTypewriter()
    {
        if (_typeCoroutine != null) StopCoroutine(_typeCoroutine);
        if (dialogueText != null) dialogueText.text = _lines[_lineIndex];
        _isTyping = false;
        if (nextIndicator != null) nextIndicator.SetActive(true);
    }

    private void EndDialogue()
    {
        _isActive = false;
        StartCoroutine(HidePanel());
        GameManager.Instance?.SetState(GameManager.GameState.Playing);
    }

    // ============================================================
    private IEnumerator ShowPanel()
    {
        if (dialoguePanel == null) yield break;
        dialoguePanel.interactable   = true;
        dialoguePanel.blocksRaycasts = true;
        float t = 0f;
        while (t < showDuration) { t += Time.unscaledDeltaTime; dialoguePanel.alpha = t / showDuration; yield return null; }
        dialoguePanel.alpha = 1f;
    }

    private IEnumerator HidePanel()
    {
        if (dialoguePanel == null) yield break;
        float t = 0f;
        while (t < showDuration) { t += Time.unscaledDeltaTime; dialoguePanel.alpha = 1f - t / showDuration; yield return null; }
        dialoguePanel.alpha          = 0f;
        dialoguePanel.interactable   = false;
        dialoguePanel.blocksRaycasts = false;
    }
}
