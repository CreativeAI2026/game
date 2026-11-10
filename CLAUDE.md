# CLAUDE.md

## ブランチ・コミット・push・PR

- `<種別>/<内容>` のブランチを切る（種別は `feature` / `fix` / `docs` / `chore`、内容は英小文字 + ハイフン）。
- コミットメッセージは日本語で、何をしたかが分かる 1 行にする。
- コミットにも PR 本文にも Claude の署名を付けない。
- push する前に、ブランチ名が実際の変更内容に合っているか確認する。合っていなければ `git branch -m <新しい名前>` で付け直す（push 済みなら付け直さない）。
- push する前に最新の `main` を rebase で取り込む（`git fetch origin && git rebase origin/main`）。
- push する前に CSharpier の整形チェック（`mise exec -- dotnet csharpier check .`）と `.github/scripts/check-meta.sh` を通す。
- PR はテンプレートの各欄を短く埋める。`main` へは PR 経由でのみ入れる。

## 変更の後始末

- コミット前に差分を見直し、作業中に入った不要な変更（デバッグ用コード・一時ファイル・意図しない Unity の自動変更など）を消す。
- 変更によって使われなくなったコード（メソッド・フィールド・クラス・アセット）も同じ PR で消す。互換用に残したり、コメントアウトで残したりしない。
- push する前に差分をレビューし、次の点を確認して直す。
  - Unity 6 で古くなった書き方をしていないか（例: `FindObjectOfType` → `FindFirstObjectByType` / `FindAnyObjectByType`、`Rigidbody.velocity` → `linearVelocity`、旧 Input Manager → Input System）。
  - 足したファイル・アセットが本来あるべきフォルダに置かれているか。
  - 既存のクラスやヘルパーで済むものを新しく書いていないか。
  - もっと読みやすく、壊れにくい書き方がないか（責務の分け方・名前・エラー時の扱い）。

## コードのルール

### 書き方

- コメントは日本語で書く。public なクラスやメンバーには `/// <summary>` を付ける。
- Inspector で設定するフィールドは `[SerializeField] private` にし、名前は `_camelCase` にする。public フィールドにしない。
- 継承しないクラスは `sealed` にする。
- 常駐のシングルトンは `public static T Instance { get; private set; }` を `Awake` で設定する形に揃える。
- `Update` など毎フレーム呼ばれる処理で `Find` 系や `GetComponent` を呼ばない。参照は `Awake` / `Start` で取っておく。

### テスト

- ロジック（計算・ルール・データ変換）は EditMode テストで確かめる。シーンの読み込みや常駐オブジェクトなど、Play しないと確かめられないものだけ PlayMode テストにする。
- 共通のヘルパーは `Tests/Shared/` に置く。
- テストクラスは `Tests/EditMode/` か `Tests/PlayMode/` に置き、クラスに `/// <summary>` を付ける。PR に貼られるテスト結果の表は、`<summary>` の最初の 1 文（「。」まで、60 文字以内）を「内容」の列に出す。
- テストを足した PR では、結果の表に新しいテストクラスの行が出て、「内容」が埋まっていることを確認する。

## 変更してはいけないファイル

- `CLAUDE.md`・`docs/`・`.github/CODEOWNERS` は担当者（`.github/CODEOWNERS` に記載）以外は変更しない。直したい点があれば担当者に伝える。
