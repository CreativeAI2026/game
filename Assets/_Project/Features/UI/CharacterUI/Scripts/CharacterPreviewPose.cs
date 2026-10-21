using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CreativeAI.UI.CharacterUI
{
    // A transform-and-renderer-only rig. HumanPoseHandler poses this copy, never the live player.
    internal sealed class CharacterPreviewPose : IDisposable
    {
        private readonly Dictionary<Transform, Transform> _transforms = new();
        private readonly List<(Renderer source, Renderer copy)> _body = new();
        private readonly List<Renderer> _renderers = new();
        private readonly PlayerModel _source;
        private readonly Dictionary<
            Transform,
            (Vector3 position, Quaternion rotation, Vector3 scale)
        > _standing = new();
        private readonly AnimationClip _bladeIdle;
        private PlayableGraph _idleGraph;
        private Animator _previewAnimator;
        public bool IsAnimating => _idleGraph.IsValid();
        public double AnimationTime => IsAnimating ? _idleGraph.GetRootPlayable(0).GetTime() : 0;

        public void Tick(float deltaTime)
        {
            if (IsAnimating)
                _idleGraph.Evaluate(deltaTime);
        }

        private void StopIdle()
        {
            if (_idleGraph.IsValid())
                _idleGraph.Destroy();
            if (_previewAnimator != null)
                _previewAnimator.enabled = false;
            foreach (var pair in _standing)
            {
                pair.Key.localPosition = pair.Value.position;
                pair.Key.localRotation = pair.Value.rotation;
                pair.Key.localScale = pair.Value.scale;
            }
        }

        private void StartBladeIdle()
        {
            if (_bladeIdle == null)
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
            _idleGraph = PlayableGraph.Create("UI Blade Idle");
            _idleGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clip = AnimationClipPlayable.Create(_idleGraph, _bladeIdle);
            clip.SetApplyFootIK(false);
            clip.SetApplyPlayableIK(false);
            var output = AnimationPlayableOutput.Create(_idleGraph, "Pose", _previewAnimator);
            output.SetSourcePlayable(clip);
            _idleGraph.Play();
            _idleGraph.Evaluate(0);
        }

        private GameObject _weapon;
        private readonly GameObject _root;
        public Transform Root => _root.transform;
        public PlayerModel Source => _source;
        public IEnumerable<Renderer> Renderers => _renderers;
        public int WeaponId { get; private set; } = -1;

        public CharacterPreviewPose(
            PlayerModel model,
            WeaponManager manager,
            AnimationClip bladeIdle
        )
        {
            _source = model;
            _bladeIdle = bladeIdle;
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
            SynchronizeAppearance();
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

        public float KneeBend
        {
            get
            {
                float maximum = 0;
                foreach (
                    var side in new[]
                    {
                        new[]
                        {
                            HumanBodyBones.LeftUpperLeg,
                            HumanBodyBones.LeftLowerLeg,
                            HumanBodyBones.LeftFoot,
                        },
                        new[]
                        {
                            HumanBodyBones.RightUpperLeg,
                            HumanBodyBones.RightLowerLeg,
                            HumanBodyBones.RightFoot,
                        },
                    }
                )
                {
                    var upper = _transforms[_source.Animator.GetBoneTransform(side[0])];
                    var lower = _transforms[_source.Animator.GetBoneTransform(side[1])];
                    var foot = _transforms[_source.Animator.GetBoneTransform(side[2])];
                    maximum = Mathf.Max(
                        maximum,
                        Vector3.Angle(
                            lower.position - upper.position,
                            foot.position - lower.position
                        )
                    );
                }
                return maximum;
            }
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
                copy.sharedMaterials = source.sharedMaterials;
                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block);
                copy.SetPropertyBlock(block);
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
            if (_weapon != null)
                _renderers.AddRange(_weapon.GetComponentsInChildren<Renderer>(true));
        }

        public void SetWeapon(GameObject source, int id)
        {
            if (_weapon != null)
            {
                _weapon.SetActive(false);
                UnityEngine.Object.Destroy(_weapon);
                _weapon = null;
            }
            StopIdle();
            WeaponId = source != null ? id : -1;
            if (WeaponId == 1001)
                StartBladeIdle();
            if (source == null)
            {
                SynchronizeAppearance();
                return;
            }
            PlayerSocketId socketId = id switch
            {
                1002 => PlayerSocketId.BowGrip,
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
                copy.localPosition = source.localPosition;
                copy.localRotation = source.localRotation;
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
