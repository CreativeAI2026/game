#!/usr/bin/env python3
"""NUnit3 の結果 XML を GitHub Actions のジョブサマリ用 Markdown（日本語）に変換する。

使い方:
    python3 .github/scripts/test-summary.py <ラベル> <結果XMLのパス...> >> "$GITHUB_STEP_SUMMARY"

XML が無い（＝コンパイルエラー等でテストまで到達しなかった）場合もその旨を出力し、
「何も出ないので状況が分からない」状態を作らない。
"""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def class_of(tc):
    """test-case からクラス名（名前空間なし）を取り出す。

    `fullname` をドットで割ると `TestCase(0.5f, -1)` のような引数付きテストで
    引数の中の小数点まで区切りに使われてしまう（`0f,-1` のような行が出る）ので、
    NUnit が持っている `classname` を優先し、無い場合だけ引数部分を落として推定する。
    """
    cls = tc.get("classname")
    if not cls:
        full = tc.get("fullname") or tc.get("name") or "?"
        cls = full.split("(", 1)[0].rsplit(".", 1)[0]
    return cls.rsplit(".", 1)[-1] or "?"


def collect(root):
    """test-case を (クラス名 -> 集計) と、失敗/スキップの明細に畳む。"""
    per_class = {}
    failures = []
    skipped = []

    for tc in root.iter("test-case"):
        full = tc.get("fullname") or tc.get("name") or "?"
        cls = class_of(tc)
        result = tc.get("result") or "?"

        stats = per_class.setdefault(cls, {"total": 0, "passed": 0, "failed": 0, "skipped": 0})
        stats["total"] += 1

        if result == "Passed":
            stats["passed"] += 1
        elif result in ("Failed", "Error"):
            stats["failed"] += 1
            failures.append((full, message_of(tc)))
        else:
            stats["skipped"] += 1
            skipped.append((full, message_of(tc)))

    return per_class, failures, skipped


def message_of(tc):
    for m in tc.iter("message"):
        text = (m.text or "").strip()
        if text:
            return " ".join(text.split())
    return ""


def render(label, path):
    lines = []
    if not Path(path).exists():
        lines.append(f"## {label}")
        lines.append("")
        lines.append(f"⚠️ 結果ファイルがありません（`{path}`）。")
        lines.append("")
        lines.append("テストが実行される前に失敗している可能性があります（コンパイルエラー・ライセンス等）。")
        lines.append("ジョブのログを確認してください。")
        return "\n".join(lines)

    root = ET.parse(path).getroot()
    per_class, failures, skipped = collect(root)

    total = sum(s["total"] for s in per_class.values())
    passed = sum(s["passed"] for s in per_class.values())
    failed = sum(s["failed"] for s in per_class.values())
    skip = sum(s["skipped"] for s in per_class.values())
    duration = root.get("duration") or "?"

    icon = "❌" if failed else ("⚠️" if skip else "✅")
    headline = (
        f"{icon} **{total} 件中 {passed} 件成功**"
        f"（失敗 {failed} / スキップ {skip} / 所要 {duration} 秒）"
    )

    lines.append(f"## {label}")
    lines.append("")
    lines.append(headline)
    lines.append("")
    lines.append("| テストクラス | 件数 | 成功 | 失敗 | スキップ | 判定 |")
    lines.append("| --- | ---: | ---: | ---: | ---: | :---: |")
    for cls in sorted(per_class):
        s = per_class[cls]
        mark = "❌" if s["failed"] else ("⚠️" if s["skipped"] else "✅")
        lines.append(
            f"| {cls} | {s['total']} | {s['passed']} | {s['failed']} | {s['skipped']} | {mark} |"
        )
    lines.append("")

    if failures:
        lines.append("### ❌ 失敗したテスト")
        lines.append("")
        for name, msg in failures:
            lines.append(f"- `{name}`")
            if msg:
                lines.append(f"  - {msg[:300]}")
        lines.append("")

    if skipped:
        lines.append("### ⚠️ スキップされたテスト")
        lines.append("")
        for name, msg in skipped:
            lines.append(f"- `{name}`")
            if msg:
                lines.append(f"  - {msg[:300]}")
        lines.append("")

    return "\n".join(lines)


def main():
    if len(sys.argv) < 3:
        print("usage: test-summary.py <ラベル> <結果XML...>", file=sys.stderr)
        return 2

    label = sys.argv[1]
    # game-ci は artifactsPath 配下に複数の XML を吐くことがあるので、全部まとめる。
    for path in sys.argv[2:]:
        print(render(label, path))
        print()
    return 0


if __name__ == "__main__":
    sys.exit(main())
