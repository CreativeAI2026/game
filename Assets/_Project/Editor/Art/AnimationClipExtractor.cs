using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// FBX 内のアニメーションクリップ(読み取り専用)を、編集できる .anim として FBX と同じフォルダへ書き出す。
    ///
    /// FBX のクリップは Animation ウィンドウでイベントを付けられず、Import Settings の Events は
    /// フレーム送りや関数名の選択ができないため、.anim に書き出して Animation ウィンドウで編集する運用にする。
    /// - 既に .anim がある場合はカーブ・設定だけを FBX の内容で上書きし、イベントと GUID は残す
    ///   (FBX が更新されたら選び直して再実行すれば、付けたイベントを失わずに取り込める)
    /// - プロジェクト内の AnimatorController が元の FBX クリップを参照していれば、書き出した .anim に差し替える
    ///
    /// 使い方: Project ウィンドウで FBX を展開してクリップを選び(複数可)、右クリック →
    /// CreativeAI → 「FBXのクリップを .anim に書き出す(イベント保持)」。
    /// </summary>
    public static class AnimationClipExtractor
    {
        private const string MenuPath = "Assets/CreativeAI/FBXのクリップを .anim に書き出す(イベント保持)";

        // FBX に含まれる、Unity が内部で使うプレビュー用クリップの名前の接頭辞
        private const string PreviewClipPrefix = "__preview__";

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is AnimationClip clip && IsModelClip(clip))
                    return true;
            }
            return false;
        }

        [MenuItem(MenuPath)]
        private static void Extract()
        {
            var replacements = new Dictionary<AnimationClip, AnimationClip>();
            foreach (Object obj in Selection.objects)
            {
                if (obj is not AnimationClip source || !IsModelClip(source))
                    continue;
                replacements[source] = WriteClip(source);
            }

            AssetDatabase.SaveAssets();
            ReplaceControllerReferences(replacements);
            AssetDatabase.SaveAssets();
        }

        private static bool IsModelClip(AnimationClip clip) =>
            AssetDatabase.IsSubAsset(clip)
            && !clip.name.StartsWith(PreviewClipPrefix)
            && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is ModelImporter;

        /// <summary>source を .anim に書き出す。既存の .anim があればイベントを残してカーブ等を上書きする。</summary>
        private static AnimationClip WriteClip(AnimationClip source)
        {
            string modelPath = AssetDatabase.GetAssetPath(source);
            // Ctrl+D で複製したときと同じ命名("Armature|Slash_01" → "Armature_Slash_01.anim")
            string clipName = source.name.Replace('|', '_');
            string path = $"{System.IO.Path.GetDirectoryName(modelPath)}/{clipName}.anim".Replace('\\', '/');

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AnimationClip created = Object.Instantiate(source);
                created.name = clipName;
                AssetDatabase.CreateAsset(created, path);
                Debug.Log($"[AnimationClipExtractor] {path} を作成しました", created);
                return created;
            }

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(existing);
            Undo.RecordObject(existing, "Update Clip From Model");
            EditorUtility.CopySerialized(source, existing);
            existing.name = clipName;
            AnimationUtility.SetAnimationEvents(existing, events);
            EditorUtility.SetDirty(existing);
            Debug.Log(
                $"[AnimationClipExtractor] {path} を FBX の内容で更新しました(イベント {events.Length} 件は保持)",
                existing
            );
            return existing;
        }

        /// <summary>全 AnimatorController のステート・Blend Tree で、元の FBX クリップを書き出した .anim に差し替える。</summary>
        private static void ReplaceControllerReferences(Dictionary<AnimationClip, AnimationClip> replacements)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController"))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    AssetDatabase.GUIDToAssetPath(guid)
                );
                if (controller == null)
                    continue;

                foreach (AnimatorControllerLayer layer in controller.layers)
                    ReplaceInStateMachine(layer.stateMachine, replacements, controller);
            }
        }

        private static void ReplaceInStateMachine(
            AnimatorStateMachine stateMachine,
            Dictionary<AnimationClip, AnimationClip> replacements,
            AnimatorController controller
        )
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                AnimatorState state = child.state;
                if (state.motion is AnimationClip clip && replacements.TryGetValue(clip, out AnimationClip replaced))
                {
                    Undo.RecordObject(state, "Replace Motion");
                    state.motion = replaced;
                    EditorUtility.SetDirty(state);
                    Debug.Log(
                        $"[AnimationClipExtractor] {controller.name} のステート {state.name} を {replaced.name} に差し替えました",
                        controller
                    );
                }
                else if (state.motion is BlendTree tree)
                {
                    ReplaceInBlendTree(tree, replacements, controller);
                }
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
                ReplaceInStateMachine(child.stateMachine, replacements, controller);
        }

        private static void ReplaceInBlendTree(
            BlendTree tree,
            Dictionary<AnimationClip, AnimationClip> replacements,
            AnimatorController controller
        )
        {
            // children は配列のコピーが返るため、書き換えた配列を代入し直す
            ChildMotion[] children = tree.children;
            bool changed = false;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion is AnimationClip clip && replacements.TryGetValue(clip, out AnimationClip replaced))
                {
                    children[i].motion = replaced;
                    changed = true;
                    Debug.Log(
                        $"[AnimationClipExtractor] {controller.name} の Blend Tree 内の {clip.name} を {replaced.name} に差し替えました",
                        controller
                    );
                }
                else if (children[i].motion is BlendTree subTree)
                {
                    ReplaceInBlendTree(subTree, replacements, controller);
                }
            }

            if (changed)
            {
                Undo.RecordObject(tree, "Replace Motion");
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
        }
    }
}
