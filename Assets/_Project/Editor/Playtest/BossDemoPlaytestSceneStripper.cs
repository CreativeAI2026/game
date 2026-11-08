using CreativeAI.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// ボス体験用ビルド(<see cref="BossDemoPlaytestBuild"/>)の最中だけ、ビルドに入るシーンのコピーからデバッグ用の部品を外す。
    /// 元のシーン・プレハブは変えないので、エディターではこれまでどおりデバッグできる。
    /// 焦り度の検知(PanicDetector)とボスの追い込みは残し、記録(CSV)と表示パネルだけを外す。
    /// </summary>
    public sealed class BossDemoPlaytestSceneStripper : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // 再生時(report が null)や、通常・CI のビルドでは何もしない。
            if (report == null || !BossDemoPlaytestBuild.IsBuilding)
                return;

            // 焦り度の CSV 記録・記録中の表示・振り返りパネル
            Strip<PanicSessionLogger>(scene, removeGameObject: false);
            // 焦り度の表示パネル(専用のキャンバスごと)
            Strip<PanicDebugUI>(scene, removeGameObject: true);
            Strip<KurageDebugControls>(scene, removeGameObject: false);
            // 体験用ビルドに無いシーンへ飛ぶデバッグ用のキー
            Strip<DebugSceneJumpInput>(scene, removeGameObject: false);
        }

        private static void Strip<T>(Scene scene, bool removeGameObject)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(includeInactive: true))
                {
                    string label = $"{scene.name}/{component.gameObject.name} の {typeof(T).Name}";
                    if (CanDestroy(component, removeGameObject))
                    {
                        Object.DestroyImmediate(
                            removeGameObject ? component.gameObject : (Object)component
                        );
                        Debug.Log($"[BossDemoPlaytestBuild] {label} を外しました");
                    }
                    else
                    {
                        // プレハブの中身は消せないので、オブジェクトごと止める。
                        component.gameObject.SetActive(false);
                        Debug.Log(
                            $"[BossDemoPlaytestBuild] {label} をオブジェクトごと無効にしました"
                        );
                    }
                }
            }
        }

        private static bool CanDestroy(Component component, bool removeGameObject)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(component))
                return true;
            return removeGameObject
                ? PrefabUtility.IsAddedGameObjectOverride(component.gameObject)
                : PrefabUtility.IsAddedComponentOverride(component);
        }
    }
}
