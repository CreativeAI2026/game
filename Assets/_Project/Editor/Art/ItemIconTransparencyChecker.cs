using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools.Art
{
    /// <summary>
    /// アイテムアイコンPNG(Art/UI/Items 配下)の「背景透過の精度」を一括チェックするツール。
    /// 差し替え候補(透過PNG)がちゃんと透過しているか・白フチ(ハロー)が残っていないかを
    /// Console にレポートする。読み取りはインポート後の圧縮テクスチャではなく、
    /// 元PNGのバイト列を直接デコードして行う(圧縮の影響を排除して真のアルファを見るため)。冪等・非破壊。
    /// </summary>
    public static class ItemIconTransparencyChecker
    {
        // チェック対象フォルダ(アイテムアイコンの置き場)。武器プレースホルダ(他班素材)は対象外。
        private static readonly string[] TargetFolders =
        {
            "Assets/_Project/Art/UI/Items/Food",
            "Assets/_Project/Art/UI/Items/Equipment",
            "Assets/_Project/Art/UI/Items/Important",
        };

        // --- 判定しきい値 ---------------------------------------------------
        private const byte OpaqueA = 250; // これ以上を「不透明」とみなす
        private const byte TransparentA = 10; // これ以下を「透明」とみなす

        // 半透明(エッジ)ピクセルのうち、RGBがこの値(0-255)を全チャンネル超える=白っぽい
        private const int WhiteChannel = 200;

        // 半透明ピクセルが全体のこの割合以上あって初めてハロー判定を意味あるとみなす
        private const double SemiMeaningfulRatio = 0.001;

        // 半透明ピクセル中の白っぽい割合がこれを超えたらハロー疑い
        private const double HaloSuspectRatio = 0.30;

        // 不透明率がこれを超えたら「背景が抜けていない疑い」
        private const double NoCutoutOpaqueRatio = 0.98;

        [MenuItem("Tools/CreativeAI/Art/Check Item Icon Transparency")]
        public static void Check()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", TargetFolders);
            if (guids.Length == 0)
            {
                Debug.LogWarning(
                    "[IconChecker] 対象フォルダにPNGが見つかりません。パスを確認してください:\n  "
                        + string.Join("\n  ", TargetFolders)
                );
                return;
            }

            var results = new List<Result>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                results.Add(Analyze(path));
            }

            results.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            Report(results);
        }

        private struct Result
        {
            public string Name;
            public string Path;
            public bool Decoded;
            public bool IsSprite; // インポート設定が Sprite(2D and UI) か
            public bool AlphaIsTransparency; // Import: Alpha Is Transparency
            public bool HasAlpha; // a<255 のピクセルが1つでもあるか
            public double OpaqueRatio;
            public double TransparentRatio;
            public double SemiRatio;
            public double HaloRatio; // 半透明中の白っぽい割合
            public bool WhiteBleed; // 透明域(a≈0)なのにRGBが白く残っている(にじみ源)
            public string Verdict; // OK / 要確認 / NG
            public string Reason;
        }

        private static Result Analyze(string assetPath)
        {
            var r = new Result { Name = Path.GetFileName(assetPath), Path = assetPath };

            // インポート設定
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter imp)
            {
                r.IsSprite = imp.textureType == TextureImporterType.Sprite;
                r.AlphaIsTransparency = imp.alphaIsTransparency;
            }

            // 元PNGを直接デコード(圧縮の影響を排除)
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
            }
            catch (System.Exception e)
            {
                r.Reason = "読込失敗: " + e.Message;
                r.Verdict = "NG";
                return r;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                Object.DestroyImmediate(tex);
                r.Reason = "PNGデコード失敗";
                r.Verdict = "NG";
                return r;
            }

            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            r.Decoded = true;

            long total = px.Length;
            long opaque = 0,
                transparent = 0,
                semi = 0,
                semiWhite = 0,
                transWhite = 0;

            foreach (var p in px)
            {
                if (p.a >= OpaqueA)
                {
                    opaque++;
                }
                else if (p.a <= TransparentA)
                {
                    transparent++;
                    if (p.r > WhiteChannel && p.g > WhiteChannel && p.b > WhiteChannel)
                        transWhite++;
                }
                else
                {
                    semi++;
                    if (p.r > WhiteChannel && p.g > WhiteChannel && p.b > WhiteChannel)
                        semiWhite++;
                }
            }

            r.HasAlpha = opaque < total; // 完全不透明でない=どこかに透過あり
            r.OpaqueRatio = (double)opaque / total;
            r.TransparentRatio = (double)transparent / total;
            r.SemiRatio = (double)semi / total;
            r.HaloRatio = semi > 0 ? (double)semiWhite / semi : 0.0;
            // 透明域の過半が白RGB=にじみ源(alphaIsTransparency ONなら実害は小だが要注意)
            r.WhiteBleed = transparent > 0 && (double)transWhite / transparent > 0.5;

            // --- 判定 -------------------------------------------------------
            if (!r.HasAlpha)
            {
                r.Verdict = "NG";
                r.Reason = "アルファ無し(背景が透過していない=白背景のまま)";
                return r;
            }

            var reasons = new List<string>();
            if (r.OpaqueRatio > NoCutoutOpaqueRatio)
                reasons.Add($"不透明率{r.OpaqueRatio:P1}(背景が殆ど抜けていない疑い)");
            if (r.SemiRatio > SemiMeaningfulRatio && r.HaloRatio > HaloSuspectRatio)
                reasons.Add($"白フチ疑い(半透明中の白{r.HaloRatio:P0})");
            if (!r.IsSprite)
                reasons.Add("Import設定がSprite(2D and UI)でない");
            if (!r.AlphaIsTransparency)
                reasons.Add("Import: Alpha Is Transparency がOFF(フチ滲みの原因)");
            if (r.WhiteBleed)
                reasons.Add("透明域のRGBが白(にじみ源。alphaIsTransparency ONで軽減)");

            if (reasons.Count == 0)
            {
                r.Verdict = "OK";
                r.Reason = "";
            }
            else
            {
                r.Verdict = "要確認";
                r.Reason = string.Join(" / ", reasons);
            }
            return r;
        }

        private static void Report(List<Result> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[IconChecker] アイテムアイコン透過チェック  ({results.Count}枚)");
            sb.AppendLine(
                "  凡例: 透明率=完全透明px割合 / 不透明率=不透明px割合 / 白フチ=半透明px中の白っぽい割合"
            );
            sb.AppendLine(
                "  ------------------------------------------------------------------------------"
            );
            sb.AppendLine(
                string.Format(
                    "  {0,-6} {1,-32} {2,7} {3,7} {4,7}  {5}",
                    "判定",
                    "ファイル",
                    "透明率",
                    "不透明",
                    "白フチ",
                    "備考"
                )
            );

            int ng = 0,
                warn = 0,
                ok = 0;
            foreach (var r in results)
            {
                if (r.Verdict == "NG")
                    ng++;
                else if (r.Verdict == "要確認")
                    warn++;
                else
                    ok++;

                sb.AppendLine(
                    string.Format(
                        "  {0,-6} {1,-32} {2,7:P1} {3,7:P1} {4,7:P0}  {5}",
                        r.Verdict,
                        Truncate(r.Name, 32),
                        r.TransparentRatio,
                        r.OpaqueRatio,
                        r.HaloRatio,
                        r.Reason
                    )
                );
            }

            sb.AppendLine(
                "  ------------------------------------------------------------------------------"
            );
            sb.AppendLine($"  合計: OK {ok} / 要確認 {warn} / NG {ng}");

            if (ng > 0)
                Debug.LogError(sb.ToString());
            else if (warn > 0)
                Debug.LogWarning(sb.ToString());
            else
                Debug.Log(sb.ToString());
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
