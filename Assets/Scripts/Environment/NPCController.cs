using UnityEngine;
using System.Collections;

/// <summary>
/// NPC - 話しかけると会話開始。簡単なパトロール・アイドルモーション付き。
/// </summary>
public class NPCController : InteractableBase
{
    [Header("NPC Info")]
    public string npcName = "村人";
    public Sprite npcPortrait;

    [Header("Dialogue")]
    [TextArea(2, 6)]
    public string[] dialogueLines;

    [Header("Motion")]
    public bool   canPatrol     = false;
    public float  idleTurnSpeed = 20f;

    [Header("References")]
    public DialogueUI dialogueUI;

    private Animator _anim;
    private Transform _playerTransform;

    // ============================================================
    protected override void Start()
    {
        base.Start();
        promptMessage = $"F: {npcName} に話しかける";
        _anim = GetComponentInChildren<Animator>();

        var p = FindAnyObjectByType<PlayerController>();
        if (p != null) _playerTransform = p.transform;
    }

    protected override void Update()
    {
        base.Update();

        // アイドル時にプレイヤーの方を向く
        if (_playerTransform != null && GameManager.Instance?.IsPlaying == true)
        {
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist < 5f)
            {
                Vector3 dir = (_playerTransform.position - transform.position).WithY(0f);
                if (dir.magnitude > 0.1f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(dir),
                        idleTurnSpeed * Time.deltaTime
                    );
            }
        }
    }

    protected override void OnInteract(PlayerStats player)
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;
        if (dialogueUI == null) dialogueUI = FindAnyObjectByType<DialogueUI>();
        if (dialogueUI == null) return;

        dialogueUI.StartDialogue(npcName, npcPortrait, dialogueLines);
        GameManager.Instance?.SetState(GameManager.GameState.Dialogue);
    }
}
