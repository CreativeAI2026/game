using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの頭上のわっか(天使のわっか)を投げる攻撃の、わっか自体の動き。
    /// 触手で掴む → 山なりで投げ上げる(落下地点を音で知らせる) → プレイヤーの胸の高さまで垂直に落ちる → その高さで少し待つ → 必ず半径を縮めて締める。
    /// 降りた時に内側にいたプレイヤーは、待つ間は輪の外へ出られない。締めた時に内側にいれば拘束し、いなければ締めた形のまま少し待ってから頭上へ戻る。
    ///
    /// プレイヤーの拘束(操作の制限・レバガチャ・締め付けダメージ)は <see cref="KurageRingRestraint"/> の担当。
    /// わっかは締めている間プレイヤーに付いて行き、振りほどかれたら広がってから頭上へ戻る。
    /// わっかのメッシュは原点が輪の中心に無いため、動かす間は輪の中心に置いた Pivot の子にして扱う。
    /// </summary>
    public class KurageHaloRing : MonoBehaviour
    {
        private enum Phase
        {
            OnHead,
            Held,
            Rising,
            Hovering,
            Dropping,
            ChestHover,
            Squeezing,
            Restraining,
            MissHold,
            Breaking,
            Returning,
        }

        [Header("参照")]
        [Tooltip("頭上のわっか(天使のわっか)に付けた RingRadiusDeformer")]
        [SerializeField]
        private RingRadiusDeformer _halo;

        [Tooltip("わっかを掴む触手の先端(1本目)。投げるまでの間、2本の真ん中にわっかを置く")]
        [SerializeField]
        private Transform _gripA;

        [Tooltip("わっかを掴む触手の先端(2本目)")]
        [SerializeField]
        private Transform _gripB;

        [Header("投げ上げ")]
        [Tooltip("投げ上げの最高点の、落下地点からの高さ（m）")]
        [SerializeField]
        private float _apexHeight = 25f;

        [Tooltip("投げてから最高点に届くまで（秒）")]
        [SerializeField]
        private float _riseDuration = 1.0f;

        [Tooltip("最高点で止まっている時間（秒）。投げてから落ちるまでが避ける時間になる")]
        [SerializeField]
        private float _hoverDuration = 0.5f;

        [Tooltip("最高点からプレイヤーの胸の高さまで落ちる時間（秒）")]
        [SerializeField]
        private float _dropDuration = 0.3f;

        [Tooltip("触手に掴まれている間、触手の先端へ寄っていく速さ。大きいほどぴったり付く")]
        [SerializeField]
        private float _gripFollowSharpness = 12f;

        [Header("締め付け")]
        [Tooltip("締め付けた時の輪の半径（m）。プレイヤーの胴回りに合わせる")]
        [SerializeField]
        private float _restraintRadius = 0.55f;

        [Tooltip("締め付ける高さ。プレイヤーの足元からの高さ（m）")]
        [SerializeField]
        private float _restraintHeight = 1.5f;

        [Tooltip(
            "胸の高さまで降りてから締め始めるまで待つ時間（秒）。降りた時に内側にいたプレイヤーは、この間は輪の外へ出られない"
        )]
        [SerializeField]
        private float _chestHoverDuration = 0.6f;

        [Tooltip("締め始めてから締め切るまで（秒）")]
        [SerializeField]
        private float _squeezeDuration = 0.2f;

        [Tooltip("捕まえられなかった時、締めた形のまま待ってから頭上へ戻るまで（秒）")]
        [SerializeField]
        private float _missHoldDuration = 0.5f;

        [Tooltip("振りほどかれて広がる時間（秒）")]
        [SerializeField]
        private float _breakDuration = 0.25f;

        [Tooltip("頭上へ戻る時間（秒）")]
        [SerializeField]
        private float _returnDuration = 1.0f;

        private KurageBossSounds _sounds;
        private Transform _pivot;
        private Transform _headParent;
        private Vector3 _headLocalPosition;
        private Quaternion _headLocalRotation;
        private Vector3 _headCenterInParent;

        private Phase _phase = Phase.OnHead;
        private float _timer;
        private Vector3 _from;
        private Vector3 _landing;
        private float _fromRadius;
        private Transform _player;
        private CharacterController _playerBody;

        // 輪の内側にいるか。胸の高さに降りた時に内側なら待つ間は外へ出さない。締め始めた時に内側なら拘束する
        private bool _playerInside;
        private Vector3 _squeezeFrom;

        /// <summary>締め始めた時に内側にプレイヤーがいた。引数はプレイヤー。</summary>
        public event Action<Transform> Caught;

        /// <summary>締め始めた時に内側にプレイヤーがいなかった。</summary>
        public event Action Missed;

        /// <summary>頭上へ戻り切った。</summary>
        public event Action ReturnedToHead;

        public bool IsOnHead => _phase == Phase.OnHead;

        private void Awake()
        {
            Assert.IsNotNull(_halo, $"{name}: わっか(RingRadiusDeformer)が設定されていません");
            Assert.IsNotNull(_gripA, $"{name}: わっかを掴む触手の先端(1本目)が設定されていません");
            Assert.IsNotNull(_gripB, $"{name}: わっかを掴む触手の先端(2本目)が設定されていません");
            _sounds = GetComponent<KurageBossSounds>();
            Assert.IsNotNull(_sounds, $"{name}: KurageBossSounds が同じ GameObject にありません");

            Transform haloTransform = _halo.transform;
            _headParent = haloTransform.parent;
            _headLocalPosition = haloTransform.localPosition;
            _headLocalRotation = haloTransform.localRotation;
            _pivot = new GameObject($"{haloTransform.name}_Pivot").transform;
        }

        private void Start()
        {
            // 輪の中心は RingRadiusDeformer の Awake で決まるので、全員の Awake が終わった後に取る
            _headCenterInParent = _headParent.InverseTransformPoint(_halo.WorldCenter);
        }

        private void OnDestroy()
        {
            if (_pivot != null)
                Destroy(_pivot.gameObject);
        }

        /// <summary>触手がわっかを掴んだ(アニメーションイベント)。投げるまで触手の先端に付いて行く。</summary>
        public void Grab()
        {
            if (_phase != Phase.OnHead)
                return;
            DetachFromHead();
            EnterPhase(Phase.Held);
        }

        /// <summary>落下地点へ向けて投げ上げる。掴む前に呼ばれた場合も頭上から投げる。</summary>
        public void Throw(Vector3 landing, Transform player)
        {
            if (_phase == Phase.OnHead)
                DetachFromHead();
            else if (_phase != Phase.Held)
                return;

            _from = _pivot.position;
            _landing = landing;
            _player = player;
            _playerBody = player != null ? player.GetComponent<CharacterController>() : null;
            _playerInside = false;
            EnterPhase(Phase.Rising);

            _sounds.PlayRingThrow(_from);
            _sounds.PlayRingWarning(landing);
        }

        /// <summary>締めているわっかを振りほどかれた。広がってから頭上へ戻る。</summary>
        public void Break()
        {
            if (_phase is not (Phase.Squeezing or Phase.Restraining))
                return;
            _fromRadius = _halo.CurrentRadius;
            EnterPhase(Phase.Breaking);
            _sounds.PlayRingBreak(_pivot.position);
        }

        /// <summary>今の場所から頭上へ戻す(攻撃の中断・プレイヤーが倒れた時など)。</summary>
        public void ReturnToHead()
        {
            if (_phase is Phase.OnHead or Phase.Returning)
                return;
            _from = _pivot.position;
            _fromRadius = _halo.CurrentRadius;
            EnterPhase(Phase.Returning);
        }

        private void LateUpdate()
        {
            // 掴んでいる触手の先端はアニメーションで動くので、Animator が姿勢を書いた後に追う
            _timer += Time.deltaTime;
            switch (_phase)
            {
                case Phase.Held:
                    Vector3 grip = (_gripA.position + _gripB.position) * 0.5f;
                    _pivot.position = Vector3.Lerp(
                        _pivot.position,
                        grip,
                        1f - Mathf.Exp(-_gripFollowSharpness * Time.deltaTime)
                    );
                    break;

                case Phase.Rising:
                    UpdateRising();
                    break;

                case Phase.Hovering:
                    if (_timer >= _hoverDuration)
                        EnterPhase(Phase.Dropping);
                    break;

                case Phase.Dropping:
                    UpdateDropping();
                    break;

                case Phase.ChestHover:
                    if (_playerInside)
                        ConfinePlayer();
                    if (_timer >= _chestHoverDuration)
                        StartSqueeze();
                    break;

                case Phase.Squeezing:
                    UpdateSqueezing();
                    break;

                case Phase.MissHold:
                    if (_timer >= _missHoldDuration)
                        ReturnToHead();
                    break;

                case Phase.Restraining:
                    FollowPlayer();
                    break;

                case Phase.Breaking:
                    float spread = Mathf.Clamp01(_timer / _breakDuration);
                    _halo.SetRadius(Mathf.Lerp(_fromRadius, _halo.OriginalRadius, spread));
                    if (spread >= 1f)
                        ReturnToHead();
                    break;

                case Phase.Returning:
                    UpdateReturning();
                    break;
            }
        }

        private void UpdateRising()
        {
            float t = Mathf.Clamp01(_timer / _riseDuration);
            Vector3 apex = _landing + Vector3.up * _apexHeight;

            // 水平は一定の速さ、高さは上がるほど減速させて山なりに見せる
            Vector3 position = Vector3.Lerp(_from, apex, t);
            position.y = Mathf.Lerp(_from.y, apex.y, 1f - (1f - t) * (1f - t));
            _pivot.position = position;

            if (t >= 1f)
                EnterPhase(Phase.Hovering);
        }

        private void UpdateDropping()
        {
            float t = Mathf.Clamp01(_timer / _dropDuration);
            Vector3 position = _landing;
            position.y = Mathf.Lerp(_landing.y + _apexHeight, _landing.y + _restraintHeight, t * t);
            _pivot.position = position;

            if (t >= 1f)
                ArriveAtChestHeight();
        }

        /// <summary>胸の高さまで降りた。ここで内側にいたプレイヤーは、締めるまで輪の外へ出られない。</summary>
        private void ArriveAtChestHeight()
        {
            _sounds.PlayRingLand(_pivot.position);
            _playerInside = IsPlayerInside();
            EnterPhase(Phase.ChestHover);
        }

        /// <summary>中にプレイヤーがいてもいなくても、必ず締める。</summary>
        private void StartSqueeze()
        {
            _fromRadius = _halo.CurrentRadius;
            _squeezeFrom = _pivot.position;
            // 待つ間に自分から輪の中へ入ったプレイヤーも捕まえる(締める輪が体をすり抜けて見えないように)
            _playerInside = IsPlayerInside();
            EnterPhase(Phase.Squeezing);
            _sounds.PlayRingSqueeze(_pivot.position);

            if (_playerInside)
                Caught?.Invoke(_player);
            else
                Missed?.Invoke();
        }

        private void UpdateSqueezing()
        {
            float squeeze = Mathf.Clamp01(_timer / _squeezeDuration);
            _halo.SetRadius(Mathf.Lerp(_fromRadius, _restraintRadius, squeeze * squeeze));

            // 捕まえた時は、締めながら輪をプレイヤーの胸へ寄せる(輪の中心とプレイヤーの位置はずれているため)
            if (_playerInside && _player != null)
            {
                Vector3 chest = _player.position + Vector3.up * _restraintHeight;
                _pivot.position = Vector3.Lerp(
                    _squeezeFrom,
                    chest,
                    Mathf.SmoothStep(0f, 1f, squeeze)
                );
            }

            if (squeeze < 1f)
                return;
            EnterPhase(_playerInside ? Phase.Restraining : Phase.MissHold);
        }

        private bool IsPlayerInside()
        {
            if (_player == null)
                return false;
            Vector3 offset = _player.position - _pivot.position;
            offset.y = 0f;
            // 輪の管にかかっているだけなら外とみなす(ぎりぎりで逃げ切った扱い)
            float inner = _halo.CurrentRadius - _halo.TubeRadius;
            return offset.sqrMagnitude <= inner * inner;
        }

        /// <summary>輪を壁として、内側にいるプレイヤーを外へ出さない(輪の内側まで押し戻す)。</summary>
        private void ConfinePlayer()
        {
            if (_player == null)
                return;
            float bodyRadius = _playerBody != null ? _playerBody.radius : 0f;
            float limit = Mathf.Max(0f, _halo.CurrentRadius - _halo.TubeRadius - bodyRadius);
            Vector3 offset = _player.position - _pivot.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= limit || distance < 1e-4f)
                return;

            Vector3 push = offset * (limit / distance) - offset;
            if (_playerBody != null && _playerBody.enabled)
                _playerBody.Move(push);
            else
                _player.position += push;
        }

        private void FollowPlayer()
        {
            if (_player == null)
                return;
            _pivot.position = _player.position + Vector3.up * _restraintHeight;
        }

        private void UpdateReturning()
        {
            float t = Mathf.SmoothStep(0f, 1f, _timer / _returnDuration);
            Vector3 head = _headParent.TransformPoint(_headCenterInParent);
            _pivot.position = Vector3.Lerp(_from, head, t);
            _halo.SetRadius(Mathf.Lerp(_fromRadius, _halo.OriginalRadius, t));

            if (_timer >= _returnDuration)
                AttachToHead();
        }

        private void DetachFromHead()
        {
            // 輪の中心に Pivot を置き、見た目の位置を変えずにわっかをその子にする
            _pivot.SetPositionAndRotation(_halo.WorldCenter, Quaternion.identity);
            _halo.transform.SetParent(_pivot, true);
        }

        private void AttachToHead()
        {
            Transform haloTransform = _halo.transform;
            haloTransform.SetParent(_headParent, false);
            haloTransform.SetLocalPositionAndRotation(_headLocalPosition, _headLocalRotation);
            _halo.ResetRadius();
            EnterPhase(Phase.OnHead);
            ReturnedToHead?.Invoke();
        }

        private void EnterPhase(Phase phase)
        {
            _phase = phase;
            _timer = 0f;
        }
    }
}
