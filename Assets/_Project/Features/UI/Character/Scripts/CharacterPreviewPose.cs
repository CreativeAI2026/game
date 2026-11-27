using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CreativeAI.UI
{
    // Isolated render rig; only its own Animator and PlayableGraph drive preview poses.
    internal sealed class CharacterPreviewPose : IDisposable
    {
        /// <summary>弓の武器 ID。持つ手のソケットと攻撃の流し方が他の武器と違う。</summary>
        public const int BowWeaponId = 1002;

        private readonly Dictionary<Transform, Transform> _transforms = new();
        private readonly List<(Renderer source, Renderer copy)> _body = new();
        private readonly List<Renderer> _renderers = new();
        private readonly PlayerModel _source;
        private readonly Dictionary<
            Transform,
            (Vector3 position, Quaternion rotation, Vector3 scale)
        > _standing = new();
        private readonly AnimationClip _unarmedIdle;
        private readonly AnimationClip _weaponIdle;
        private PlayableGraph _idleGraph;
        private Animator _previewAnimator;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _attack;

        // 攻撃の後半に続けて流すクリップ(弓の引き絞り→発射)。切り替え済み、または無ければ null。
        private AnimationClip _followUp;
        private float _attackElapsed;
        private float _attackDuration;
        public bool IsAttacking => _attack.IsValid();

        /// <summary>いま流している攻撃クリップ。攻撃していなければ null。</summary>
        public AnimationClip AttackClip => IsAttacking ? _attack.GetAnimationClip() : null;
        public bool IsAnimating => _idleGraph.IsValid();
        public double AnimationTime => IsAnimating ? _idleGraph.GetRootPlayable(0).GetTime() : 0;

        public void Tick(float deltaTime)
        {
            if (!IsAnimating)
                return;
            if (IsAttacking)
            {
                _attackElapsed += deltaTime;
                if (_attackElapsed >= _attackDuration)
                    EndAttack();
                else
                {
                    if (_followUp != null && _attackElapsed >= AttackClip.length)
                        StartFollowUp(deltaTime);
                    _mixer.SetInputWeight(
                        1,
                        Mathf.Min(
                            Mathf.Clamp01(_attackElapsed / .1f),
                            Mathf.Clamp01((_attackDuration - _attackElapsed) / .15f)
                        )
                    );
                }
            }
            _mixer.SetInputWeight(0, 1f - (IsAttacking ? _mixer.GetInputWeight(1) : 0f));
            _idleGraph.Evaluate(deltaTime);
        }

        /// <summary>待機から攻撃を1回流す。<paramref name="followUp"/> があれば <paramref name="clip"/> の直後に続ける。</summary>
        public bool PlayAttack(AnimationClip clip, AnimationClip followUp = null)
        {
            if (!IsAnimating || IsAttacking || clip == null || clip.length <= 0)
                return false;
            ConnectAttack(clip);
            _followUp = followUp;
            _attackElapsed = 0;
            _attackDuration = clip.length + (followUp != null ? followUp.length : 0);
            _mixer.SetInputWeight(1, 0);
            return true;
        }

        private void ConnectAttack(AnimationClip clip)
        {
            _attack = AnimationClipPlayable.Create(_idleGraph, clip);
            _attack.SetApplyFootIK(false);
            _attack.SetApplyPlayableIK(false);
            _idleGraph.Connect(_attack, 0, _mixer, 1);
        }

        private void DisconnectAttack()
        {
            _mixer.DisconnectInput(1);
            _idleGraph.DestroyPlayable(_attack);
            _attack = default;
        }

        private void StartFollowUp(float deltaTime)
        {
            float firstLength = AttackClip.length;
            DisconnectAttack();
            ConnectAttack(_followUp);
            // この直後の Evaluate が deltaTime だけ進めるので、その分を引いた時刻から始める。
            _attack.SetTime(_attackElapsed - firstLength - deltaTime);
            _followUp = null;
        }

        private void EndAttack()
        {
            if (!IsAttacking)
                return;
            DisconnectAttack();
            _mixer.SetInputWeight(1, 0);
            _mixer.SetInputWeight(0, 1);
        }

        private void StopIdle()
        {
            if (_idleGraph.IsValid())
                _idleGraph.Destroy();
            _attack = default;
            _mixer = default;
            if (_previewAnimator != null)
                _previewAnimator.enabled = false;
            foreach (var pair in _standing)
            {
                pair.Key.SetLocalPositionAndRotation(pair.Value.position, pair.Value.rotation);
                pair.Key.localScale = pair.Value.scale;
            }
        }

        private void StartIdle(AnimationClip idle)
        {
            if (idle == null)
                return;
            if (_previewAnimator == null)
            {
                _previewAnimator = _root.AddComponent<Animator>();
                _previewAnimator.avatar = _source.Animator.avatar;
                _previewAnimator.applyRootMotion = false;
                _previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _previewAnimator.fireEvents = false;
            }
            _previewAnimator.enabled = true;
            _idleGraph = PlayableGraph.Create("UI Weapon Preview");
            _idleGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clip = AnimationClipPlayable.Create(_idleGraph, idle);
            clip.SetApplyFootIK(false);
            clip.SetApplyPlayableIK(false);
            var output = AnimationPlayableOutput.Create(_idleGraph, "Pose", _previewAnimator);
            _mixer = AnimationMixerPlayable.Create(_idleGraph, 2);
            _idleGraph.Connect(clip, 0, _mixer, 0);
            _mixer.SetInputWeight(0, 1);
            output.SetSourcePlayable(_mixer);
            _idleGraph.Play();
            _idleGraph.Evaluate(0);
        }

        private GameObject _weapon;
        private readonly GameObject _root;
        private readonly List<Material> _materials = new();
        private readonly List<Renderer> _weaponRenderers = new();
        private readonly MaterialPropertyBlock _block = new();
        public Transform Root => _root.transform;
        public PlayerModel Source => _source;

        /// <summary>
        /// 描画対象のコピー。同じリストを使い回し、中身だけ <see cref="SynchronizeAppearance"/> で更新する。
        /// </summary>
        public IReadOnlyList<Renderer> Renderers => _renderers;
        public int WeaponId { get; private set; } = -1;

        /// <summary>
        /// 画面に収める範囲(<see cref="Root"/> のローカル座標)。素手の立ち姿の体だけから1回決める。
        /// 武器や動きを含めないので、持ち替えてもキャラクターの大きさと位置は変わらない。
        /// </summary>
        public Bounds Framing { get; }

        public CharacterPreviewPose(
            PlayerModel model,
            WeaponManager manager,
            AnimationClip unarmedIdle,
            AnimationClip weaponIdle
        )
        {
            _source = model;
            _unarmedIdle = unarmedIdle;
            _weaponIdle = weaponIdle;
            _root = new GameObject("UI Character Pose") { hideFlags = HideFlags.DontSave };
            _root.transform.position = new Vector3(0, -20000, 0);
            UnityEngine.Object.DontDestroyOnLoad(_root);
            CopyTree(model.transform, _root.transform, _transforms, true);
            foreach (var source in model.GetComponentsInChildren<Renderer>(true))
            {
                bool weapon = false;
                for (int i = 0; i < manager.WeaponCount; i++)
                {
                    var root = manager.GetWeaponObject(1001 + i);
                    if (root != null && source.transform.IsChildOf(root.transform))
                    {
                        weapon = true;
                        break;
                    }
                }
                if (weapon)
                    continue;
                var copy = CopyRenderer(source, _transforms[source.transform], _transforms);
                if (copy != null)
                    _body.Add((source, copy));
            }
            if (model.Animator.avatar != null && model.Animator.avatar.isHuman)
            {
                using var handler = new HumanPoseHandler(model.Animator.avatar, Root);
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                Array.Clear(pose.muscles, 0, pose.muscles.Length);
                pose.bodyPosition = Vector3.up;
                pose.bodyRotation = Quaternion.identity;
                SetMuscle(ref pose, "Left Arm Down-Up", -.9f);
                SetMuscle(ref pose, "Right Arm Down-Up", -.9f);
                handler.SetHumanPose(ref pose);
                Straighten(
                    HumanBodyBones.LeftUpperLeg,
                    HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot,
                    Vector3.down
                );
                Straighten(
                    HumanBodyBones.RightUpperLeg,
                    HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot,
                    Vector3.down
                );
                Straighten(
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand,
                    Vector3.down
                );
                Straighten(
                    HumanBodyBones.RightUpperArm,
                    HumanBodyBones.RightLowerArm,
                    HumanBodyBones.RightHand,
                    Vector3.down
                );
            }
            foreach (var copy in _transforms.Values)
                _standing[copy] = (copy.localPosition, copy.localRotation, copy.localScale);
            SetLayer(_root.transform, ModelPreview.Layer);
            SynchronizeAppearance();
            Framing = BodyBounds();
            // 範囲は立ち姿で決めてから、素手の待機を始める(武器を持たせるまでは SetWeapon が呼ばれない)。
            StartIdle(_unarmedIdle);
        }

        // 作成直後(Root が単位回転・単位スケール)にだけ呼ぶ。
        private Bounds BodyBounds()
        {
            Bounds bounds = default;
            bool first = true;
            foreach (var (_, copy) in _body)
            {
                var box = WorldBounds(copy);
                if (first)
                    bounds = box;
                else
                    bounds.Encapsulate(box);
                first = false;
            }
            bounds.center -= Root.position;
            return bounds;
        }

        // SkinnedMeshRenderer.bounds は描画を切っている間は更新されないので、localBounds とルートボーンから求める。
        private static Bounds WorldBounds(Renderer renderer)
        {
            if (renderer is not SkinnedMeshRenderer skin)
                return renderer.bounds;
            var matrix = (
                skin.rootBone != null ? skin.rootBone : skin.transform
            ).localToWorldMatrix;
            var local = skin.localBounds;
            var box = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner =
                    local.center
                    + Vector3.Scale(
                        local.extents,
                        new Vector3(
                            (i & 1) == 0 ? -1 : 1,
                            (i & 2) == 0 ? -1 : 1,
                            (i & 4) == 0 ? -1 : 1
                        )
                    );
                box.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }
            return box;
        }

        private static void SetLayer(Transform node, int layer)
        {
            node.gameObject.layer = layer;
            foreach (Transform child in node)
                SetLayer(child, layer);
        }

        private void Straighten(
            HumanBodyBones upperId,
            HumanBodyBones lowerId,
            HumanBodyBones endId,
            Vector3 direction
        )
        {
            var upperSource = _source.Animator.GetBoneTransform(upperId);
            var lowerSource = _source.Animator.GetBoneTransform(lowerId);
            var endSource = _source.Animator.GetBoneTransform(endId);
            if (upperSource == null || lowerSource == null || endSource == null)
                return;
            var upper = _transforms[upperSource];
            var lower = _transforms[lowerSource];
            var end = _transforms[endSource];
            Quaternion endRotation = end.rotation;
            upper.rotation =
                Quaternion.FromToRotation(lower.position - upper.position, direction)
                * upper.rotation;
            lower.rotation =
                Quaternion.FromToRotation(end.position - lower.position, direction)
                * lower.rotation;
            if (endId == HumanBodyBones.LeftFoot || endId == HumanBodyBones.RightFoot)
                end.rotation = endRotation;
        }

        private static void SetMuscle(ref HumanPose pose, string name, float value)
        {
            int index = Array.IndexOf(HumanTrait.MuscleName, name);
            if (index >= 0)
                pose.muscles[index] = value;
        }

        public void SynchronizeAppearance()
        {
            _renderers.Clear();
            foreach (var (source, copy) in _body)
            {
                if (source == null || copy == null)
                    continue;
                source.GetSharedMaterials(_materials);
                copy.SetSharedMaterials(_materials);
                source.GetPropertyBlock(_block);
                copy.SetPropertyBlock(_block);
                copy.enabled = source.enabled && source.gameObject.activeInHierarchy;
                if (source is SkinnedMeshRenderer skin && copy is SkinnedMeshRenderer posed)
                {
                    posed.sharedMesh = skin.sharedMesh;
                    if (skin.sharedMesh != null)
                        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                            posed.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                }
                _renderers.Add(copy);
            }
            _renderers.AddRange(_weaponRenderers);
        }

        public void SetWeapon(GameObject source, int id)
        {
            if (_weapon != null)
            {
                _weapon.SetActive(false);
                UnityEngine.Object.Destroy(_weapon);
                _weapon = null;
            }
            _weaponRenderers.Clear();
            StopIdle();
            WeaponId = source != null ? id : -1;
            if (source == null)
            {
                StartIdle(_unarmedIdle);
                SynchronizeAppearance();
                return;
            }
            StartIdle(_weaponIdle);
            PlayerSocketId socketId = id switch
            {
                BowWeaponId => PlayerSocketId.BowGrip,
                1003 => PlayerSocketId.ScytheGrip,
                1004 => PlayerSocketId.HammerGrip,
                _ => PlayerSocketId.SwordGrip,
            };
            var sourceSocket = _source.GetSocket(socketId);
            if (sourceSocket == null || !_transforms.TryGetValue(sourceSocket, out var socket))
                return;
            _weapon = new GameObject("Preview Weapon " + id);
            _weapon.transform.SetParent(socket, false);
            _weapon.transform.localScale = source.transform.localScale;
            var map = new Dictionary<Transform, Transform>(_transforms);
            CopyTree(source.transform, _weapon.transform, map, true);
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                bool visible = renderer.enabled;
                for (var node = renderer.transform; node != source.transform; node = node.parent)
                    if (!node.gameObject.activeSelf)
                        visible = false;
                if (visible)
                    CopyRenderer(renderer, map[renderer.transform], map);
            }
            SetLayer(_weapon.transform, ModelPreview.Layer);
            _weapon.GetComponentsInChildren(true, _weaponRenderers);
            SynchronizeAppearance();
        }

        private static void CopyTree(
            Transform source,
            Transform copy,
            Dictionary<Transform, Transform> map,
            bool root
        )
        {
            map[source] = copy;
            if (!root)
            {
                copy.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
                copy.localScale = source.localScale;
            }
            foreach (Transform child in source)
            {
                var node = new GameObject(child.name).transform;
                node.SetParent(copy, false);
                CopyTree(child, node, map, false);
            }
        }

        private static Renderer CopyRenderer(
            Renderer source,
            Transform target,
            Dictionary<Transform, Transform> map
        )
        {
            Renderer copy;
            if (source is SkinnedMeshRenderer skin)
            {
                var posed = target.gameObject.AddComponent<SkinnedMeshRenderer>();
                posed.sharedMesh = skin.sharedMesh;
                var bones = skin.bones;
                for (int i = 0; i < bones.Length; i++)
                    bones[i] =
                        bones[i] != null && map.TryGetValue(bones[i], out var bone) ? bone : null;
                posed.bones = bones;
                if (skin.rootBone != null && map.TryGetValue(skin.rootBone, out var root))
                    posed.rootBone = root;
                posed.localBounds = skin.localBounds;
                posed.updateWhenOffscreen = true;
                copy = posed;
            }
            else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter))
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                copy = target.gameObject.AddComponent<MeshRenderer>();
            }
            else
                return null;
            copy.sharedMaterials = source.sharedMaterials;
            copy.forceRenderingOff = true;
            return copy;
        }

        public void Dispose()
        {
            if (_idleGraph.IsValid())
                _idleGraph.Destroy();
            if (_root != null)
            {
                _root.SetActive(false);
                UnityEngine.Object.Destroy(_root);
            }
        }
    }
}
