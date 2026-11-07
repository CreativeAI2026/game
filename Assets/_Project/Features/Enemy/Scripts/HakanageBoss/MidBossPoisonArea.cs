using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 液体着弾後に生成される毒付与エリア。
    /// 一定間隔で OverlapBox を使い、エリア内のプレイヤーに毒を付与し続ける。
    /// 持続時間終了後は当たり判定のみ先に無効化し、Decal Projectorをフェードアウトしてから破棄する。
    /// </summary>
    public class MidBossPoisonArea : MonoBehaviour
    {
        [Header("判定設定")]
        [Tooltip("毒エリアの半径")]
        [SerializeField]
        private float _radius = 1.5f;

        [Tooltip("判定の厚み（Y軸方向・地面から上への高さ）")]
        [SerializeField]
        private float _thickness = 0.5f;

        [Tooltip("判定の中心をY軸方向にずらす量（通常は厚みの半分）")]
        [SerializeField]
        private float _yOffset = 0.25f;

        [Header("フェードアウト設定")]
        [Tooltip("当たり判定消失後にDecalをフェードアウトさせる時間（秒）")]
        [SerializeField]
        private float _fadeOutDuration = 1.5f;

        // DecalProjectorのfadeFactorをリフレクションで操作する（アセンブリ参照不要）
        private Component _decalProjector;
        private System.Reflection.PropertyInfo _fadeProp;

        private float _duration;
        private float _poisonDuration;
        private float _poisonDamagePerTick;
        private float _poisonTickInterval;
        private float _interval;
        private float _lifetimeTimer;
        private float _intervalTimer;
        private bool _isActive;

        private void Awake()
        {
            // アセンブリの文字列指定による取得失敗（即座に消える原因）を防ぐため、確実な探索方式に変更
            Component[] comps = GetComponentsInChildren<Component>();
            foreach (var c in comps)
            {
                if (c.GetType().Name == "DecalProjector")
                {
                    _decalProjector = c;
                    _fadeProp = c.GetType().GetProperty("fadeFactor");
                    break;
                }
            }
        }

        private LayerMask _playerLayer;

        public void Initialize(
            float duration,
            float interval,
            float poisonDuration,
            float poisonDamagePerTick,
            float poisonTickInterval,
            LayerMask playerLayer
        )
        {
            _duration = duration;
            _interval = interval;
            _poisonDuration = poisonDuration;
            _poisonDamagePerTick = poisonDamagePerTick;
            _poisonTickInterval = poisonTickInterval;
            _playerLayer = playerLayer;
            _lifetimeTimer = 0f;
            _intervalTimer = _interval; // 生成直後に即座に判定を行う（タイムラグ排除）
            _isActive = true;
        }

        private void Update()
        {
            if (!_isActive)
                return;

            _lifetimeTimer += Time.deltaTime;

            if (_lifetimeTimer >= _duration)
            {
                // 当たり判定を先に無効化し、Decalをフェードアウトしてから破棄
                _isActive = false;
                StartCoroutine(FadeOutAndDestroy());
                return;
            }

            // 毎フレーム判定を行うが、実際の毒付与は _interval ごとに制限する
            CheckPoison();
        }

        private void CheckPoison()
        {
            // 床基準（Y軸）の判定
            Vector3 halfExtents = new Vector3(_radius, _thickness * 0.5f, _radius);
            Vector3 center = transform.position + transform.up * _yOffset;

            // LayerMaskを明示的に指定してOverlapBoxを実行
            Collider[] hits = Physics.OverlapBox(
                center,
                halfExtents,
                transform.rotation,
                _playerLayer
            );

            bool isPlayerInside = false;

            foreach (var col in hits)
            {
                // Y軸を無視した平面距離で円形に絞り込む
                Vector3 localPos = transform.InverseTransformPoint(col.transform.position);
                localPos.y = 0f;

                if (localPos.magnitude <= _radius)
                {
                    isPlayerInside = true;

                    // タイマーが満了していれば付与
                    if (_intervalTimer >= _interval)
                    {
                        var effectHandler = col.GetComponentInParent<PlayerStatusEffectHandler>();
                        if (effectHandler != null)
                        {
                            effectHandler.ApplyPoison(
                                _poisonDuration,
                                _poisonDamagePerTick,
                                _poisonTickInterval
                            );
                        }
                        // 一度付与したらタイマーをリセット
                        _intervalTimer = 0f;
                    }
                    break;
                }
            }

            if (isPlayerInside)
            {
                // エリア内にいる間はタイマーを進める（次の付与に向けて）
                _intervalTimer += Time.deltaTime;
            }
            else
            {
                // エリア外にいる間はタイマーを満了状態に保つ（次に入った瞬間に即座に付与するため）
                _intervalTimer = _interval;
            }
        }

        private IEnumerator FadeOutAndDestroy()
        {
            float elapsed = 0f;

            if (_decalProjector != null && _fadeProp != null)
            {
                while (elapsed < _fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);
                    _fadeProp.SetValue(_decalProjector, t);
                    yield return null;
                }
            }

            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 常時表示。判定が有効な間は緑、フェードアウト中は黄色
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            // Broad-phaseのBox（赤）
            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Vector3 boxCenter = new Vector3(0, _yOffset, 0);
            Vector3 boxSize = new Vector3(_radius * 2f, _thickness, _radius * 2f);
            Gizmos.DrawCube(boxCenter, boxSize);
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireCube(boxCenter, boxSize);

            // Narrow-phaseの円柱（緑 or 黄）
            Gizmos.color = _isActive ? Color.green : Color.yellow;
            int segments = 32;
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 p1_bottom = new Vector3(
                    Mathf.Cos(angle1) * _radius,
                    _yOffset - _thickness * 0.5f,
                    Mathf.Sin(angle1) * _radius
                );
                Vector3 p2_bottom = new Vector3(
                    Mathf.Cos(angle2) * _radius,
                    _yOffset - _thickness * 0.5f,
                    Mathf.Sin(angle2) * _radius
                );
                Gizmos.DrawLine(p1_bottom, p2_bottom);

                Vector3 p1_top = new Vector3(
                    Mathf.Cos(angle1) * _radius,
                    _yOffset + _thickness * 0.5f,
                    Mathf.Sin(angle1) * _radius
                );
                Vector3 p2_top = new Vector3(
                    Mathf.Cos(angle2) * _radius,
                    _yOffset + _thickness * 0.5f,
                    Mathf.Sin(angle2) * _radius
                );
                Gizmos.DrawLine(p1_top, p2_top);

                if (i % 4 == 0)
                {
                    Gizmos.DrawLine(p1_bottom, p1_top);
                }
            }

            Gizmos.matrix = oldMatrix;
        }
#endif
    }
}
