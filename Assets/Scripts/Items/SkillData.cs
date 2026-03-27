using UnityEngine;

/// <summary>
/// スキルデータ ScriptableObject
/// Assets > Create > DragonPath > Skill Data で作成
/// </summary>
[CreateAssetMenu(fileName = "New Skill", menuName = "DragonPath/Skill Data")]
public class SkillData : ScriptableObject
{
    public enum SkillType { MeleeAOE, DashSlash, Projectile, Buff, Summon }

    [Header("Basic Info")]
    public string    skillName    = "Skill Name";
    [TextArea]
    public string    description  = "Skill description.";
    public Sprite    icon;
    public SkillType skillType    = SkillType.MeleeAOE;

    [Header("Cost")]
    public float mpCost       = 20f;
    public float staminaCost  = 10f;
    public float cooldown     = 5f;
    public float castTime     = 0.2f;   // 発動までのディレイ

    [Header("Effect")]
    public float damage       = 50f;
    public float range        = 5f;
    public float duration     = 0f;     // バフなどの持続時間

    [Header("Prefabs")]
    public GameObject vfxPrefab;
    public GameObject projectilePrefab;

    [Header("Unlock")]
    public int    requiredLevel = 1;
    public string prerequisiteSkillName = "";
}
