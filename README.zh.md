# ToDoAI - AI 自动编程项目生成 Agent

一个运行在控制台的 AI Agent，能够理解自然语言需求并自动生成完整的项目代码。兼容量化等级高的模型以及没有针对 Agent 任务专门训练的模型。

## 特性

- **自然语言到代码**：用自然语言描述项目，AI 自动创建文件和目录结构
- **多轮对话**：可不断输入新需求，每次执行基于当前项目状态继续构建
- **工具化架构**：AI 选择并使用工具（write、edit、run、read、grep、mkdir、delete、tree）逐步完成任务
- **弱模型友好**：通过 `[tool] param='value'` 括号语法逐步填入参数，帮助弱模型保持正确方向
- **自动纠错循环**：每步执行后自动质量检查，失败则重新规划（可配置重试次数）
- **跨平台**：支持 Windows（cmd.exe）和 Linux/macOS（/bin/bash）
- **配置持久化**：首次启动时输入 API Key、模型、端点，保存到 `agent_config.json`

## 工具列表

| 工具 | 说明 |
|------|------|
| `write` | 创建新文件并写入完整内容 |
| `edit` | 在已有文件中替换精确文本 |
| `read` | 读取文件内容（带行号） |
| `run` | 执行终端命令 |
| `tree` | 查看目录树结构 |
| `mkdir` | 创建目录 |
| `delete` | 删除文件 |
| `grep` | 在文件中搜索文本 |

## 使用方法

```bash
cd ToDoAI
dotnet run
```

首次启动时需要配置：
- **API Key** — AI 服务商的 API 密钥
- **Model** — 模型名称（如 `gemma-4-e4b-it`、`GLM-4.7-Flash`）
- **Endpoint** — API 端点地址

后续启动自动加载 `agent_config.json`。

按提示输入项目需求。**两次空行提交**，一次空行退出。

### 示例

```
Request (two blank lines to submit, one blank line to quit):
创建一个 Python Hello World 项目。
编写 main.py，内容为 print("Hello World")，然后用 python main.py 运行

```

## 配置文件

设置保存在程序目录下的 `agent_config.json`：

```json
{
  "ApiKey": "your-api-key",
  "Model": "gemma-4-e4b-it",
  "Endpoint": "http://localhost:1234/v1/chat/completions"
}
```

删除此文件可在下次启动时重新配置。

## 项目结构

```
ToDoAI/
├── Program.cs       # 入口点、配置、事件绑定
├── ToDoList.cs      # 核心引擎：计划生成、步骤执行、重试逻辑
├── AgentTools.cs    # 工具实现
├── AIService.cs     # API 通信（流式 + 非流式）
├── Models.cs        # 数据模型与特性
├── README.md        # 英文说明
└── README.zh.md     # 本文件（英文版见 README.md）
```
