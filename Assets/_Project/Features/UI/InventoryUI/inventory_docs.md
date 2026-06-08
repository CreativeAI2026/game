

# インベントリ UI 要素（簡易）

以下はインベントリ UI に最低限必要な要素と各要点の簡潔まとめです。詳しい実装はこの要点を参照してください。

- レイアウト: グリッド（columns × rows）、スロットサイズ（例: 64px）、スロット間ギャップ（例: 8px）、レスポンシブで列数可変。
- スロット: 背景、アイコン、スタック数表示、スロット index、許容タイプ（equip/general）。Prefab と公開プロパティを定義。
- アイテムデータ: `id`, `displayName(key)`, `icon`, `type`, `stackable`, `maxStack`, `properties`（例: heal, attack）、`tags`。表示はローカライズキーを使う。
- スタッキングルール: 同種合併・タグ合併・最大数、分割（Split）UI の挙動を定義。
- ドラッグ&ドロップ: スワップ、挿入、分割。DragGhost（半透明アイコン）表示。Ctrl/Shift 修飾で分割や挿入動作。
- 入力マッピング: Primary/Secondary/Confirm/Cancel、QuickUse(n)（例: 1..8 ホットキー）。マウス/タッチ/キーボード/ゲームパッド対応。
- ツールチップ/詳細: 表示遅延（例0.35s）、最大幅、表示項目（名前/説明/スタック/レア度）、ローカライズ対応。
- コンテキスト操作: Use, Equip, Split, Drop, MoveToEquipment（右クリック/長押しで表示）。
- ホットバー: サイズ（例8）、固定表示、QuickUse 割当と連動。
- 保存・シリアライズ: フォーマット（JSON/ScriptableObject）、必須フィールド（inventoryId, slots[] {itemId,count,properties}）、schema version 管理。
- イベント/API: `OnItemAdded/Removed/Moved/Equipped`、API: `CanAddItem`, `AddItem`, `RemoveItem`, `TryMoveItem`, `GetItemAt`。
- アクセシビリティ: キーボード操作フル対応、フォーカス表示、色だけで情報を伝えない、コントラスト目標。
- パフォーマンス: Slot/DragGhost のプール、Canvas バッチ分割、仮想化（大量アイテム時の表示最適化）。
- デバッグ/テスト: `GiveItem`, `ClearInventory`, ローカライズ破壊テスト、FPS/GC のパフォーマンステスト。
- Unity 実装ヒント: `InventoryConfig` ScriptableObject（columns, rows, slotPrefab, slotSize, hotbarSize, stackingRule）、`Slot.prefab`（Image + TextMeshPro を想定）、Addressables 推奨、`InventoryInputModule` で入力統一。

---
この簡潔版を `inventory_docs.md` に上書きしました。もっと詳細な Prefab フィールドや具体的なキー名を入れますか？

---
更新・具体的なコンポーネント仕様（例: コンテナ名、Prefab パス、TextMeshPro の使用有無など）を指示いただければ追記します。


