using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// ダメージ数値の浮き上がり演出
/// </summary>
public class DamageNumber : MonoBehaviour
{
    [Header("Settings")]
    public float riseSpeed  = 1.5f;
    public float lifetime   = 0.9f;
    public float spread     = 0.5f;

    private TMP_Text _text;
    private float    _timer;
    private Vector3  _drift;
    private Camera   _cam;

    // ============================================================
    private void Awake()
    {
        _text = GetComponentInChildren<TMP_Text>();
        _cam  = Camera.main;
    }

    public void Setup(float damage, bool isCrit)
    {
        _timer = 0f;
        _drift = new Vector3(Random.Range(-spread, spread), riseSpeed, 0f);

        if (_text != null)
        {
            _text.text     = isCrit ? $"<b>{Mathf.RoundToInt(damage)}</b>" : Mathf.RoundToInt(damage).ToString();
            _text.color    = isCrit ? Color.yellow : Color.white;
            _text.fontSize = isCrit ? 28 : 22;
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / lifetime;

        transform.position += _drift * Time.deltaTime;

        // フェードアウト
        if (_text != null)
        {
            Color c   = _text.color;
            c.a       = 1f - t;
            _text.color = c;
        }

        // カメラの方向を向く
        if (_cam != null)
            transform.LookAt(transform.position + _cam.transform.forward);

        if (_timer >= lifetime) Destroy(gameObject);
    }

    // ============================================================
    // Static Factory
    // ============================================================
    private static GameObject _prefab;

    public static void Spawn(Vector3 worldPos, float damage, bool isCrit)
    {
        // プレハブが未設定の場合は動的生成
        var go  = new GameObject("DmgNum");
        go.transform.position = worldPos;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one * 0.01f;

        var dn   = go.AddComponent<DamageNumber>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform);
        textGo.transform.localPosition = Vector3.zero;
        var tmp  = textGo.AddComponent<TextMeshPro>();
        tmp.alignment  = TextAlignmentOptions.Center;
        tmp.fontSize   = 24;
        tmp.color      = Color.white;

        dn._text = tmp;
        dn.Setup(damage, isCrit);
    }
}
