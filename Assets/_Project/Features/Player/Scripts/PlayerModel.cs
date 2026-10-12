using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの「モデル層」の窓口。仮モデル・本番モデルを差し替える単位のルートに置く(Animator と同じ GameObject)。
    ///
    /// ロジック層(PlayerController 等)はボーン名やボーンの軸を直接知らず、ここを経由して
    /// - Animator
    /// - Humanoid のボーン(<see cref="GetBone"/>。ボーン名が違っても同じ部位が取れる)
    /// - 取り付け点(<see cref="GetSocket"/>。武器の持ち手など。位置・向きはモデルごとにここで調整)
    /// - 頭の注視ターゲット(<see cref="LookAtTarget"/>)
    /// を受け取る。モデルを差し替えるときは、新しいモデルにこのコンポーネントを付けてソケットを合わせるだけでよい。
    ///
    /// 前提: Avatar は Humanoid(Rig の Animation Type = Humanoid)。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class PlayerModel : MonoBehaviour
    {
        [Serializable]
        private struct SocketEntry
        {
            public PlayerSocketId id;
            public Transform socket;
        }

        [Tooltip(
            "取り付け点の一覧。コンポーネントの ⋮ →「不足しているソケットを自動生成」で既定のボーンの下に作られる。"
                + "位置・向きはソケットの Transform で調整する"
        )]
        [SerializeField]
        private SocketEntry[] _sockets = Array.Empty<SocketEntry>();

        [Tooltip("頭の注視ターゲット(Animation Rigging の Multi-Aim の Source)。HeadLookController が毎フレーム動かす")]
        [SerializeField]
        private Transform _lookAtTarget;

        private Animator _animator;

        // 見た目の高さオフセットの基準(モデルの元の置き位置)
        private Vector3 _baseLocalPosition;

        // ロジック層の Awake から呼ばれることがあり、このコンポーネントの Awake より先になりうるため遅延取得する
        public Animator Animator => _animator != null ? _animator : _animator = GetComponent<Animator>();

        public Transform LookAtTarget => _lookAtTarget;

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            Assert.IsNotNull(Animator.avatar, $"{name}: Animator に Avatar が設定されていません");
            Assert.IsTrue(Animator.avatar.isHuman, $"{name}: Avatar が Humanoid ではありません");
            Assert.IsNotNull(_lookAtTarget, $"{name}: 頭の注視ターゲット(_lookAtTarget)が未設定です");
            ValidateAnimatorParameters();
        }

        /// <summary>
        /// Controller に必須パラメータが揃っているかを検査する。名前の打ち間違いや消し忘れは
        /// SetTrigger 等が黙って無視されるため、起動時にまとめてエラーにする。
        /// </summary>
        private void ValidateAnimatorParameters()
        {
            AnimatorControllerParameter[] actual = Animator.parameters;
            foreach (var (paramName, type) in PlayerAnimatorParams.Required)
            {
                bool found = Array.Exists(actual, p => p.name == paramName && p.type == type);
                if (!found)
                    Debug.LogError(
                        $"{name}: Animator Controller にパラメータ「{paramName}」({type}) がありません。"
                            + "PlayerAnimatorParams と Animator の設定を揃えてください",
                        this
                    );
            }
        }

        /// <summary>
        /// プレイヤー配下の任意のコンポーネントから、そのプレイヤーのモデルを取得する。
        /// 武器が実行時にソケットへ付け替えられた後でも、ルートの PlayerController を経由して辿れる。
        /// </summary>
        public static PlayerModel Of(Component member)
        {
            var root = member.GetComponentInParent<PlayerController>(true);
            Assert.IsNotNull(root, $"{member.name}: 親階層に PlayerController が見つかりません");
            var model = root.GetComponentInChildren<PlayerModel>(true);
            Assert.IsNotNull(model, $"{root.name}: 子階層に PlayerModel が見つかりません");
            return model;
        }

        /// <summary>
        /// 見た目(モデル)だけを元の位置から上へずらす。0 で元に戻る。
        /// ルート(CharacterController)は動かさないので、接地判定・被弾判定・重力には影響しない。
        /// 跳躍演出のように、物理的に浮かせると接地判定と干渉する動きに使う。
        /// </summary>
        public void SetVisualHeightOffset(float height)
        {
            transform.localPosition = _baseLocalPosition + Vector3.up * height;
        }

        /// <summary>Humanoid のボーンを取得する(ボーン名に依存しない)。</summary>
        public Transform GetBone(HumanBodyBones bone)
        {
            Transform t = Animator.GetBoneTransform(bone);
            Assert.IsNotNull(t, $"{name}: Humanoid ボーン {bone} が Avatar に割り当てられていません");
            return t;
        }

        public Transform GetSocket(PlayerSocketId id)
        {
            foreach (var entry in _sockets)
            {
                if (entry.id == id && entry.socket != null)
                    return entry.socket;
            }
            Debug.LogError(
                $"{name}: ソケット {id} が登録されていません。PlayerModel の ⋮ →「不足しているソケットを自動生成」を実行してください"
            );
            return null;
        }

        /// <summary>
        /// ソケットを自動生成するときの既定の親(武器リグのボーン名)。
        /// 武器リグのボーンはアニメーションで手からの角度が付くため、その子に置けば武器がアニメーション通りに振られる。
        /// モデルにこの名前のボーンが無い場合は <see cref="DefaultBoneOf"/> の手のボーンへ置く。
        /// </summary>
        public static string DefaultRigBoneNameOf(PlayerSocketId id) =>
            id switch
            {
                PlayerSocketId.SwordGrip => "Weapon_Root",
                PlayerSocketId.BowGrip => "Bow_Root",
                PlayerSocketId.ScytheGrip => "Weapon_Root",
                PlayerSocketId.ArrowHold => "Arrow_Root",
                PlayerSocketId.HammerGrip => "Weapon_Root",
                _ => throw new ArgumentOutOfRangeException(nameof(id), id, "既定の武器リグボーンが未定義です"),
            };

        /// <summary>武器リグのボーンが無いモデルで、ソケットを自動生成するときの親ボーン。</summary>
        public static HumanBodyBones DefaultBoneOf(PlayerSocketId id) =>
            id switch
            {
                PlayerSocketId.SwordGrip => HumanBodyBones.RightHand,
                PlayerSocketId.BowGrip => HumanBodyBones.LeftHand,
                PlayerSocketId.ScytheGrip => HumanBodyBones.RightHand,
                PlayerSocketId.ArrowHold => HumanBodyBones.RightHand,
                PlayerSocketId.HammerGrip => HumanBodyBones.RightHand,
                _ => throw new ArgumentOutOfRangeException(nameof(id), id, "既定の親ボーンが未定義です"),
            };

#if UNITY_EDITOR
        /// <summary>
        /// 未登録のソケットを既定の親(武器リグのボーン。無ければ手のボーン)の直下に位置・回転ゼロで作って登録する。
        /// 親の直下に同名({id}Socket)のオブジェクトが既にあれば、作らずにそれを位置・回転ごと登録する。
        /// 登録済みのものは触らない。
        /// </summary>
        [ContextMenu("不足しているソケットを自動生成")]
        private void CreateMissingSockets()
        {
            Animator animator = GetComponent<Animator>();
            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogError($"{name}: Humanoid の Avatar を Animator に設定してから実行してください");
                return;
            }

            Undo.RecordObject(this, "Create Player Sockets");
            var entries = new List<SocketEntry>(_sockets);
            foreach (PlayerSocketId id in Enum.GetValues(typeof(PlayerSocketId)))
            {
                if (entries.Exists(e => e.id == id && e.socket != null))
                    continue;

                Transform bone = FindDescendant(transform, DefaultRigBoneNameOf(id));
                if (bone == null)
                {
                    bone = animator.GetBoneTransform(DefaultBoneOf(id));
                    if (bone == null)
                    {
                        Debug.LogError($"{name}: {DefaultBoneOf(id)} ボーンが見つからないため {id} を作れませんでした");
                        continue;
                    }
                    Debug.LogWarning(
                        $"{name}: 武器リグのボーン {DefaultRigBoneNameOf(id)} が無いため、{id} を {bone.name} の下に作ります。"
                            + "武器はアニメーションで振られません",
                        bone
                    );
                }

                string socketName = $"{id}Socket";
                Transform socket = bone.Find(socketName);
                if (socket != null)
                {
                    Debug.Log($"{name}: 既存の {socketName} を {id} として登録しました", socket);
                }
                else
                {
                    var go = new GameObject(socketName);
                    Undo.RegisterCreatedObjectUndo(go, "Create Player Sockets");
                    go.transform.SetParent(bone, false);
                    socket = go.transform;
                    Debug.Log($"{name}: ソケット {socketName} を {bone.name} の下に作成しました", socket);
                }

                entries.RemoveAll(e => e.id == id);
                entries.Add(new SocketEntry { id = id, socket = socket });
            }
            _sockets = entries.ToArray();
            EditorUtility.SetDirty(this);
        }

        private static Transform FindDescendant(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;
                Transform found = FindDescendant(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }
#endif
    }
}
