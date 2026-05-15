# ToDoAI - AI Agent for Automated Project Generation

[中文版说明](README.zh.md)

An intelligent AI Agent that runs in the console, capable of understanding natural language requirements and automatically generating complete project code. Designed for compatibility with highly quantized models or models not specifically trained for agent tasks.

## Features

- **Natural Language to Code**: Describe your project in plain language, and the AI creates the files and structure
- **Multi-turn Conversation**: Enter new requirements in a loop, each run builds on the previous project state
- **Tool-Based Architecture**: The AI selects and uses tools (write, edit, run, read, grep, mkdir, delete, tree) to accomplish tasks step by step
- **Weak Model Friendly**: Step-by-step parameter filling with bracket syntax `[tool] param='value'` helps less capable models stay on track
- **Self-Correction Loop**: Automatic quality check after each step; replans on failure (configurable retries)
- **Cross-Platform**: Windows (cmd.exe) and Linux/macOS (/bin/bash) supported
- **Config Persistence**: API key, model, and endpoint saved to `agent_config.json` on first launch

## Tools

| Tool | Description |
|------|-------------|
| `write` | Create a new file with complete content |
| `edit` | Replace exact text in an existing file |
| `read` | Read file content with line numbers |
| `run` | Execute terminal commands |
| `tree` | Show directory tree structure |
| `mkdir` | Create a directory |
| `delete` | Delete a file |
| `grep` | Search for text in files |

## Usage

```bash
cd ToDoAI
dotnet run
```

On first launch, you will be prompted for:
- **API Key** — Your AI provider's API key
- **Model** — The model name (e.g., `gemma-4-e4b-it`, `GLM-4.7-Flash`)
- **Endpoint** — The API endpoint URL

On subsequent launches, settings are loaded from `agent_config.json`.

Enter your project requirements when prompted. Use two blank lines to submit, one blank line to quit.

### Example

```
Request (two blank lines to submit, one blank line to quit):
Create a Python Hello World project.
Write main.py with print("Hello World") and run it with python main.py

```

## Configuration

Settings are stored in `agent_config.json` in the program directory:

```json
{
  "ApiKey": "your-api-key",
  "Model": "gemma-4-e4b-it",
  "Endpoint": "http://localhost:1234/v1/chat/completions"
}
```

Delete this file to reconfigure on next launch.

## Project Structure

```
ToDoAI/
├── Program.cs       # Entry point, config, event wiring
├── ToDoList.cs      # Core engine: plan generation, step execution, retry logic
├── AgentTools.cs    # Tool implementations
├── AIService.cs     # API communication (streaming + non-streaming)
├── Models.cs        # Data models and attributes
└── README.md        # This file
```
