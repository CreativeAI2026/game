using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// クラゲボス(Boss02)のFBXを読み込むとき、Blender の「テクスチャ座標 > 生成」に当たる座標を UV3 に書き込む AssetPostprocessor。
    /// 生成座標は変形前のメッシュの範囲を 0〜1 に収めたもので、KurageNoiseGradient.shadergraph がノイズの入力に使う。
    /// シェーダーの Object 座標を使わないのは、スキンメッシュでは骨の動きで座標が変わり、模様が体の上を流れてしまうため。
    /// </summary>
    public sealed class KurageGeneratedCoordBaker : AssetPostprocessor
    {
        private const string TargetFolder = "Assets/_Project/Art/Models/Characters/Enemies/Boss02/";

        // KurageNoiseGradient.shadergraph の UV ノードと揃える。UV1/UV2 はライトマップ用に空けておく。
        private const int GeneratedCoordUvChannel = 3;

        // 対象の部位と、Blender で Z 軸のミラーモディファイアが付いているか。
        // ミラー側の生成座標は元の半分と同じ値になる(模様が左右対称になる)ので、Z は絶対値で扱う。
        private static readonly Dictionary<string, bool> TargetMeshes = new()
        {
            { "体", false },
            { "顔", true },
            { "顔_中", true },
        };

        public override uint GetVersion() => 1;

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(TargetFolder, System.StringComparison.Ordinal))
                return;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!TargetMeshes.TryGetValue(renderer.name, out bool mirroredZ))
                    continue;

                var mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>().sharedMesh;
                WriteGeneratedCoords(mesh, mirroredZ);
            }
        }

        /// <summary>
        /// 頂点を Blender のローカル座標へ戻し、メッシュの範囲で 0〜1 に正規化して UV へ書き込む。
        /// Unity の FBX 読み込みでは X だけが反転する(Y・Z はそのまま)ので、X を反転して戻す。
        /// </summary>
        private static void WriteGeneratedCoords(Mesh mesh, bool mirroredZ)
        {
            var vertices = mesh.vertices;
            var blenderCoords = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i];
                blenderCoords[i] = new Vector3(-v.x, v.y, mirroredZ ? Mathf.Abs(v.z) : v.z);
            }

            var min = blenderCoords[0];
            var max = blenderCoords[0];
            foreach (var c in blenderCoords)
            {
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }
            if (mirroredZ)
                min.z = 0f; // ミラーの元の半分は Z=0 の面から始まる

            var size = max - min;
            var generated = new List<Vector3>(blenderCoords.Length);
            foreach (var c in blenderCoords)
            {
                generated.Add(new Vector3(
                    SafeDivide(c.x - min.x, size.x),
                    SafeDivide(c.y - min.y, size.y),
                    SafeDivide(c.z - min.z, size.z)));
            }
            mesh.SetUVs(GeneratedCoordUvChannel, generated);
        }

        // 厚みのない平面メッシュでは範囲が 0 になるため。
        private static float SafeDivide(float value, float range) => range > Mathf.Epsilon ? value / range : 0f;
    }
}
