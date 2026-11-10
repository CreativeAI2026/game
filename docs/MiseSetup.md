# mise のインストール

入っていない場合は OS に合わせて入れる。

**macOS（Homebrew）**

```bash
brew install mise
```

**Windows**

```powershell
winget install jdx.mise
```

インストール後はターミナルを開き直し、`mise --version` で確認する。

## シェルへの組み込み（任意）

このリポジトリの手順は `mise exec -- ...` で動くので、組み込まなくてもよい。`dotnet` を直接打ちたい場合だけ行う（[公式手順](https://mise.jdx.dev/getting-started.html)）。

**macOS（zsh）**

```bash
echo 'eval "$(mise activate zsh)"' >> ~/.zshrc
```

**Windows（PowerShell）**

```powershell
# プロファイルが無ければ作る
if (-not (Test-Path $PROFILE)) { New-Item -ItemType File -Path $PROFILE -Force | Out-Null }
Add-Content $PROFILE '(&mise activate pwsh) | Out-String | Invoke-Expression'

# プロファイルの読み込みを許可する（初回のみ）
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

新しいシェルを開き直すと有効になる。
