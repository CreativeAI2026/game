# SoundKit

音の再生・音量設定・素材の音量そろえ（ffmpeg）をまとめた、プロジェクトに依存しない音の仕組み。
外部ライブラリ（DOTween・UniTask など）は使わない。Unity 6 以降（`Awaitable` を使うため）。

## 構成

| フォルダ | アセンブリ | 内容 |
|---|---|---|
| `Runtime/` | `SoundKit` | 再生の仕組み本体（依存なし） |
| `Runtime/UGUI/` | `SoundKit.UGUI` | uGUI ボタンの音（`com.unity.ugui` が必要。使わないなら消してよい） |
| `Editor/` | `SoundKit.EditorTools` | SoundCue のインスペクタ（試聴）、ffmpeg による音量そろえ・音声エディタ |

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
5. `.gitignore` に `/AudioLoudnessBackup/` と `/AudioEditBackup/` を足す（下の音量そろえ・音声エディタが元の素材を保存する場所）。

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
- 素材の名前を変えたり移動したりすると、保存してある元の素材（と音声エディタの保存ファイル）も自動で新しいパスへ移る。

## 音声エディタ（ffmpeg）

音素材の前後の切り取り・音程・再生速度を、Unity の中で波形を見ながら変える。音声ファイルそのものを書き換える。

- 開き方：Tools > SoundKit > 音声エディタ（Project で選んだ素材を表示する）、または素材を右クリック > SoundKit > 音声エディタで開く
- できること：
  - 前後の切り取り（波形をドラッグ、または秒数を入力）
  - 前後の無音の自動検出（「無音とみなす大きさ（dB）」と「前後に残す余白（秒）」で決める。既定は -50 dB・0.01 秒）
  - 音程（半音単位、速さはそのまま）と再生速度（音程はそのまま）
    （ffmpeg に rubberband があれば、効果音の立ち上がりを崩さない設定で使う。無ければ atempo で代用し、ウィンドウに注意を出す）
  - 切り取った端だけに掛ける短いフェード（既定 5 ミリ秒。ブツッという音を防ぐ）
  - 元の音・編集後の音の試聴（保存する前に聞ける）
- まとめて処理：素材を選んで右クリック > SoundKit > 選択した音素材の前後の無音を削る（基準は音声エディタと共通）
- 対応形式：mp3・ogg・wav（wav は PCM のみ）。音量そろえと同じく、元と同じ形式で書き出す。
- 上書きする前のファイルはプロジェクト直下の `AudioEditBackup/` に1度だけ保存し、音声エディタの「元に戻す」で戻せる。
  保存した素材を開き直すと、編集前の素材に前回の編集内容を掛け直した状態から再開する（書き出しの劣化を重ねないため）。
- 音量をそろえ済みの素材は、そろえる前の元の素材から編集する。編集した後は音量をそろえ直す（そろえ直すまで SoundCue の表示が「未反映」になる）。
