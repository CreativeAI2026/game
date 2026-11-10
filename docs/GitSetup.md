# Git のセットアップ

## Git のインストール

入っていない場合は OS に合わせて入れる。

**macOS（Homebrew）**

```bash
brew install git
```

**Windows**

```powershell
winget install Git.Git
```

インストール後はターミナルを開き直し、`git --version` で確認する。

## 名前とメールアドレスの設定

コミットの作者として記録される。GitHub アカウントのものに合わせる。

```bash
git config --global user.name "<GitHub のユーザー名>"
git config --global user.email "<GitHub に登録したメールアドレス>"
```

## SSH 鍵を GitHub に登録

クローンと push は SSH で行う。

1. 鍵を作る（パスフレーズは任意。既に `~/.ssh/id_ed25519` があれば不要）。

   ```bash
   ssh-keygen -t ed25519 -C "<GitHub に登録したメールアドレス>"
   ```

2. 公開鍵をコピーする。

   **macOS**

   ```bash
   pbcopy < ~/.ssh/id_ed25519.pub
   ```

   **Windows（PowerShell）**

   ```powershell
   Get-Content $HOME\.ssh\id_ed25519.pub | Set-Clipboard
   ```

3. GitHub の **Settings → SSH and GPG keys → New SSH key** を開き、次のように入れて **Add SSH key** を押す。
   - **Title**: どの PC の鍵か分かる名前（例: `Windows-PC`）
   - **Key type**: `Authentication Key` のまま
   - **Key**: 2. でコピーした公開鍵（`ssh-ed25519` で始まる 1 行）

4. 接続を確認する（`Hi <ユーザー名>!` と出れば OK）。

   ```bash
   ssh -T git@github.com
   ```

## Organization への参加

`CreativeAI2026` からの招待メール（または GitHub の通知）を承認する。承認しないとリポジトリをクローンできない。
