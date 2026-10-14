# SoundKit

音の再生・音量設定・素材の音量そろえ（ffmpeg）をまとめた、プロジェクトに依存しない音の仕組み。
外部ライブラリ（DOTween・UniTask など）は使わない。Unity 6 以降（`Awaitable` を使うため）。

## 構成

| フォルダ | アセンブリ | 内容 |
|---|---|---|
| `Runtime/` | `SoundKit` | 再生の仕組み本体（依存なし） |
| `Runtime/UGUI/` | `SoundKit.UGUI` | uGUI ボタンの音（`com.unity.ugui` が必要。使わないなら消してよい） |
| `Editor/` | `SoundKit.EditorTools` | SoundCue のインスペクタ（試聴）、ffmpeg による音量そろえ |

## 使い方

| やりたいこと | 使うもの |
|---|---|
| 音の定義（素材・音量・種類・距離） | `SoundCue`（Create > SoundKit > SoundCue） |
| 世界の位置で1回鳴らす（足音・衝突・ヒット） | `WorldSoundPlayer.Play(cue, position, this)` |
| 画面の音を鳴らす（ポーズ中も鳴る） | `SoundEffectPlayer.Play(cue, this)` |
| 物から鳴り続ける音 | `new LoopingSound(gameObject)` を持ち、`Play` / `Stop` |
| BGM | `BgmPlayer.Play(cue)`（Main / Overlay の2段、クロスフェード） |
| 場所全体の環境音 | `AmbiencePlayer.Play(cue)` |
| シーンごとの BGM・環境音 | `SceneAudio` をシーンに1つ置く |
| ぶつかった音 | `CollisionSound` を Rigidbody を持つ物に付ける |
| UI の選択・決定・戻る | uGUI ボタンに `UIButtonSound`、それ以外は `UISounds.PlayXxx(this)` |
| 音量設定（0～1） | `AudioVolumeSettings.Set(channel, v)` → 閉じるときに `Save()` |

未設定の音は止めずに警告を1度だけ出す（素材が揃う前でも鳴らす処理を先に組めるように）。

## 導入手順（別プロジェクトへ持ち出すとき）

1. この `SoundKit` フォルダを `.meta` ごとコピーする（GUID を保つため）。
2. AudioMixer を用意する。グループ `BGM` `Ambient` `Effect` `UI` を Master の下に作り、
   各グループと Master の Volume を Exposed Parameter にして
   `MasterVolume` `BgmVolume` `AmbientVolume` `EffectVolume` `UIVolume` と名付ける。
   （既存プロジェクトの `MainAudioMixer.mixer` を `.meta` ごとコピーしてもよい）
3. どこかの `Resources` フォルダ直下に、Create > SoundKit から
   `AudioMixerRouting` と `UISoundSet` を作り（名前は変えない）、ミキサーとグループ、UI 音を割り当てる。
4. 使う側の asmdef の references に `SoundKit`（uGUI 用を使うなら `SoundKit.UGUI` も）を足す。
5. `.gitignore` に `/AudioLoudnessBackup/` を足す（下の音量そろえが元の素材を保存する場所）。

## 素材の音量そろえ（ffmpeg）

素材ごとにバラバラな音量を、聞こえ方の大きさ（LUFS）でそろえる。音声ファイルそのものを書き換える。

- 事前に ffmpeg（`ffmpeg.exe` と `ffprobe.exe`）を入れて PATH を通す。入れた後は Unity を開き直す。
- 全体：Tools > SoundKit > ゲームで使う音素材の音量をそろえる
  （ビルドに入るシーン・Resources・全 SoundCue / UISoundSet から使われている素材が対象）
- 一部：Project ウィンドウで素材を選び、右クリック > SoundKit > 選択した音素材の音量をそろえる
- SoundCue ごと：インスペクタの「設定を素材に反映」
- 基準は `AudioLoudnessNormalizer.TargetLufs`（-20 LUFS）。SoundCue の「Loudness Offset Db」で音ごとに上げ下げする。
- 元の素材はプロジェクト直下の `AudioLoudnessBackup/Originals` に1度だけ保存し、以後はそこから作り直す（劣化が重ならない）。
- 処理済みの印は素材の `.meta`（AudioImporter.userData）に残す。何度実行しても処理済みは飛ばす。
