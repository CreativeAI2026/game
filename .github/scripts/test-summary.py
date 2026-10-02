#!/usr/bin/env python3
"""NUnit3 の結果 XML を日本語の Markdown 表に変換する。"""

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

TESTS_ROOT = Path("Assets/_Project/Tests")

_SUMMARY_RE = re.compile(
    r"///\s*<summary>(?P<body>.*?)///\s*</summary>\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public\s+|internal\s+|sealed\s+|abstract\s+|static\s+|partial\s+)*class\s+(?P<name>\w+)",
    re.S,
)


def first_sentence(body):
    depth = 0
    for i, ch in enumerate(body):
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth = max(0, depth - 1)
        elif ch == "。" and depth == 0:
            return body[:i]
    return body


def load_descriptions(root=TESTS_ROOT):
    descriptions = {}
    if not root.is_dir():
        return descriptions

    for path in sorted(root.rglob("*.cs")):
        try:
            text = path.read_text(encoding="utf-8")
        except OSError:
            continue
        for m in _SUMMARY_RE.finditer(text):
            body = re.sub(r"^\s*///\s?", "", m.group("body"), flags=re.M)
            body = re.sub(r"<[^>]+>", "", body)
            body = " ".join(body.split())
            if not body:
                continue
            sentence = first_sentence(body)
            if not sentence:
                continue
            if len(sentence) > 60:
                sentence = sentence[:59] + "…"
            descriptions.setdefault(m.group("name"), sentence.replace("|", "\\|"))
    return descriptions


def class_of(tc):
    cls = tc.get("classname")
    if not cls:
        full = tc.get("fullname") or tc.get("name") or "?"
        cls = full.split("(", 1)[0].rsplit(".", 1)[0]
    return cls.rsplit(".", 1)[-1] or "?"


def collect(root):
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


def verdict_of(stats):
    total = stats["total"]
    if stats["failed"]:
        mark = f"❌ {stats['failed']}/{total} 失敗"
        if stats["skipped"]:
            mark += f"（{stats['skipped']} スキップ）"
        return mark
    if stats["skipped"]:
        if stats["skipped"] == total:
            return f"⚠️ {total}/{total} スキップ"
        return f"⚠️ {stats['skipped']}/{total} スキップ（{stats['passed']} 成功）"
    return f"✅ {stats['passed']}/{total} 成功"


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
    descriptions = load_descriptions()
    lines.append("| テストクラス | 判定 | 内容 |")
    lines.append("| --- | :---: | --- |")
    for cls in sorted(per_class):
        lines.append(f"| {cls} | {verdict_of(per_class[cls])} | {descriptions.get(cls, '')} |")
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
    for path in sys.argv[2:]:
        mode = Path(path).stem.removesuffix("-results")
        print(render(label if "*" in mode else f"{mode} {label}", path))
        print()
    return 0


if __name__ == "__main__":
    sys.exit(main())
