# RimChat - 边缘对话 / 言行联动

[![Steam Workshop](https://img.shields.io/badge/Steam-Workshop-blue?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3683001105)
[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-blue.svg)](https://rimworldgame.com/)
[![Harmony](https://img.shields.io/badge/Dependency-Harmony-orange.svg)](https://github.com/pardeike/HarmonyRimWorld)
[![GitHub](https://img.shields.io/badge/GitHub-RimChat-181717?logo=github&logoColor=white)](https://github.com/yancy22737-sudo/RimChat)
[![Bilibili](https://img.shields.io/badge/Bilibili-当我让AI接管了游戏外交！-00A1D6?logo=bilibili&logoColor=white)](https://bilibili.com/video/BV11FRqBJEZG)

# 用语言改变游戏进程

---

> **边缘对话** - 言行联动
> RimChat 为《边缘世界》带来了全新的 AI 体验！让 AI 接管通讯台外交玩法，与角色展开沉浸式对话，你的每一次交流，都将切实影响游戏世界。

![](https://imgur.com/L83pdo4.png)

---

## 🚧 注意！模组目前正在开发中

* 尽管我们已进行了大量测试，仍可能存在未知的漏洞。
* 如果您遇到任何问题，或是有改进建议，欢迎在下方评论区留言，或是在讨论区反馈。

---

## 核心功能

### 🌍 AI 驱动行为：通讯台重做
* 派系领袖完全由 AI 智能控制
* 基于派系特性、好感度与当前局势做出动态决策
* 支持多种外交行动：索要囚犯赎金、贸易、结盟、宣战、议和等
* 支持 AI 调用游戏内行为：袭击、支援、发布任务等

![](https://i.imgur.com/83DnBDa.gif)
![](https://i.imgur.com/jfgBqCR.png)
![](https://i.imgur.com/PijIRwJ.png)

### 💬 RPG 风格的 NPC 对话
* 让一个殖民者与另一个人类 NPC 进行对话
* 基于角色性格、关系、记忆与所处环境生成动态对话
* 支持 AI 调用游戏内行为：心情与记忆、招募、恋爱、求婚、分手等

![](https://imgur.com/swQDBUK.png)
![](https://i.imgur.com/OMvMyHj.jpeg)
![](https://imgur.com/rINqlMv.png)

**如果你觉得放大后的人物太模糊，可以试试订阅以下贴图 mods:**
* [Vanilla Textures Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2016436324)
* [[TW1.6]堂丸贴图重置~服饰 Tang's~Retexture~Apparel](https://steamcommunity.com/sharedfiles/filedetails/?id=3255510656)
* [UNAGI EYE Parts Retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=3325531190)
* [UNAGI Vanilla Hairs うなぎのバニラ髪](https://steamcommunity.com/sharedfiles/filedetails/?id=3386935298)

### 📰 动态社交圈
* 实时追踪世界内的重大事件
* 派系关系变动、战争动态、生态事件等各类内容
* 通过社交圈，随时了解世界中正在发生的一切

![社交圈](https://imgur.com/xra7hNH.png)

### ⚡ 智能事件触发
* AI 会根据游戏内的情况主动触发事件
* 角色对话与外交交流可产生联动影响
* 补充原版故事叙述者的叙事内容
* 带来更具动态、更不可预测的游戏体验

![](https://i.imgur.com/Ryhl18T.png)
![](https://i.imgur.com/RNhOnAP.png)

---

### 依赖
* **[Harmony]**
* 需自行准备 AI 服务

### API 配置说明
如需使用云端 AI 服务，请在 Mod 设置中配置以下内容：
* API 密钥
* 选择 AI 服务提供商（OpenAI、DeepSeek、Google 等）
* 或使用本地 Player2 运行模型（推荐）

## 📖 快速上手
1. 进入游戏设置 → 模组设置 → RimChat
2. 配置您的 AI 服务提供商
3. 将您的 API 密钥粘贴到 RimChat 的设置中
4. 开始游戏后，Mod 将自动启用

### 推荐的免费模型
* **Player2 GPT-OSS-120B**
* 完全免费，无任何限制，无需担心达到使用额度上限
* 官方网站：[Player2](http://player2.game)

---

## 💡 灵感来源与相关推荐
本 Mod 的灵感源自经典 Mod [RimTalk](https://steamcommunity.com/sharedfiles/filedetails/?id=3551203752&searchtext=rimtalk) 和 *骑马与砍杀 2：AI 效应*.

* 欢迎查看我的另一个模组：派系编辑器，功能强大，与 RimChat 一起使用效果更佳：[Faction Editor](https://steamcommunity.com/sharedfiles/filedetails/?id=3670833973)

## 常见问题

**问：** 是否兼容 RimTalk？
**答：** 两者互不冲突。

**问：** 是否兼容 CE？
**答：** 本 Mod 不涉及任何战斗相关机制。

**问：** 可以中途加入和移除已有存档吗？
**答：** 可以。

**问：** 与其他通讯台外交 mod 兼容吗？
**答：** 兼容，可以从默认通讯台界面进入 RimChat。
![](https://i.imgur.com/6IjWTSM.png)

**问：** AI 可以自定义任务吗?
**答：** 不可以，只能调用有限的模板。

**问：** Token 消耗？
**答：** 一次对话大概在 6000~8000 左右，基本只在你发送消息时才会消耗，闲置不会持续消耗。

**问：** 对性能的影响？
**答：** 几乎没有。

**问：** AI 可以直接跟你进行物品交换或者直接交易吗？
**答：** 利用空投交易机制，如："请求空投交易，我想花 3000 银买一个海军盔甲"

**问：** 空投总是货不对板怎么办？
**答：** 尽量利用右下角的"行动菜单"发送交易报价。

**问：** 兼容叙事者相关 mod 吗？
**答：** 完全不影响，RimChat 对话触发的事件只是作为补充。

## ⚠️ AI 幻觉警告：任何没有提示的承诺/行为都可能是 AI 的幻觉，没有实质性影响

**支持语言**：英语、简体中文

---

## 📝 更新

* 最新版 v1.2.1

---

![支持](https://imgur.com/ZaSwGOR.png)

## ❤️ 支持 RimChat

* 🌟 **喜欢这个 Mod?** 欢迎好评与收藏！
* 🐛 **如果您发现了漏洞或是有建议?** 确定你是最新版本后，欢迎在下方留言
* 🐧 **QQ 交流群：1076958977** / [Steam 聊天群组](https://steamcommunity.com/chat/invite/E8saCnPa)
* ☕ **感谢支持！** [Buy me a coffee](https://afdian.com/a/yancy12138)

**Created by yancy** | [GitHub](https://github.com/yancy22737-sudo/RimChat)
[B 站视频：当我让 AI 接管了游戏外交](https://bilibili.com/video/BV11FRqBJEZG)

感谢 欲擒故纵、pcjjos 对 RimChat 的大力支持

Module documentation: [Docs/](Docs/).
