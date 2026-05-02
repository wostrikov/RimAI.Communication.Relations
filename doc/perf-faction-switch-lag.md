# 派系切换卡顿问题调查

## 问题描述

切换到个别派系（如洛斯戴欧斯德帝国）时 UI 卡顿约 1 秒，点击该派系右侧动作按钮也会卡顿。新存档无历史记录，仅部分派系有此问题。

## 性能诊断数据

### 切换逻辑耗时（`SwitchFactionInPlace`）

所有派系切换逻辑均 <1ms，**不是瓶颈**。

| 派系 | summary | bind | presence | total |
|------|---------|------|----------|-------|
| 洛斯戴欧斯德帝国 | 0.0ms | 0.1ms | 0.0ms | 0.2ms |

### 每帧渲染耗时（`DoWindowContents`）

瓶颈在 `chat` 区域渲染：

| 派系 | mem | title | list | tabs | actions | **chat** | overlay | total |
|------|-----|-------|------|------|---------|----------|---------|-------|
| 洛斯戴欧斯德帝国 | 0.0 | 0.0 | 1.6 | 0.0 | 0.6 | **1007.9ms** | 0.0 | 1010ms |
| 橙色海洋民族统一阵线 | 0.3 | 5.6 | 4.0 | 0.9 | 0.2 | **290.0ms** | 1.4 | 302ms |
| 东南伊特达公约 | 0.0 | 0.0 | 0.4 | 0.0 | 0.0 | **176.3ms** | 0.0 | 177ms |
| 格乌格乌-摩伊克 | 0.0 | 0.0 | 0.5 | 0.0 | 0.0 | **146.9ms** | 0.0 | 147ms |
| 太空企业集团 | 0.0 | 0.0 | 0.4 | 0.0 | 0.0 | **0.2ms** | 0.0 | 0.6ms |

`chat` 区域包含：`DrawDialogueMainTabs` + `DrawExpandedActions` + `DrawChatArea`

### 关键发现

- 切换逻辑（speaker 解析、session 绑定、presence 刷新）均非瓶颈
- 瓶颈在 `DrawChatArea` → 内部包含 `DrawMessages`、`DrawControlsRow`、`DrawInputArea`
- 新存档 0 条消息，`DrawMessages` 应快速返回
- 不同派系耗时差异巨大（0.2ms vs 1007ms），说明某些派系触发了额外渲染逻辑

## 已尝试的修复

### 1. Speaker 解析缓存（已应用）

**问题**：`EnsureSessionMessageSpeakers` 遍历每条消息调用 `ResolveFactionSpeakerPawn`，无 leader 的派系每条消息都走 `PawnGenerator.GeneratePawn`。

**修复**：
- 添加 `Dictionary<Faction, Pawn> factionSpeakerCache` per-faction 缓存
- `BindActiveFactionState` 时重置 `sessionFallbackFactionSpeaker`
- `EnsureSessionMessageSpeakers` 预解析一次 speaker

**结果**：切换逻辑从 ~ms 降至 <1ms，但渲染卡顿未解决。

### 2. Quest LINQ 缓存（已应用）

**问题**：`DrawFactionQuests` 每帧做 `.Where().ToList()` 分配。

**修复**：添加 `_cachedQuests` + `_cachedQuestsTick` 缓存。

**结果**：减少每帧 GC 分配，但非主要瓶颈。

### 3. Presence 刷新优化（已应用）

**问题**：`DrawFactionList` 每帧调用 `GetAvailableFactions(true)` 触发全派系 presence 刷新。

**修复**：改为 `GetAvailableFactions(false)`。

**结果**：减少每帧开销，但非主要瓶颈。

## 待定位

`chat` 区域（`DrawChatArea`）内具体哪一步耗时 1000ms。`DrawChatArea` 包含：
1. `DrawMessages` — 0 条消息应快速返回
2. `DrawControlsRow` → `DrawStrategyStatusHint` — 需检查 `BuildExpandedStrategyStatusHint` 是否昂贵
3. `DrawInputArea` — 需检查 `EvaluateSendGate` 是否昂贵

**下一步**：在 `DrawChatArea` 内部添加更细粒度的计时，定位具体耗时函数。
