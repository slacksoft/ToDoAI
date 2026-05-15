using System.Reflection;

namespace ToDoAI;

[AttributeUsage(AttributeTargets.Method)]
public class AgentToolAttribute : Attribute
{
    public string Name { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public AgentToolAttribute(string name, string displayName, string description)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
    }
}

[AttributeUsage(AttributeTargets.Parameter)]
public class AgentParamAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public string Prompt { get; }
    public AgentParamAttribute(string name, string description, string prompt = "")
    {
        Name = name;
        Description = description;
        Prompt = prompt;
    }
}

public class ToolParameter
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Prompt { get; set; } = "";
    public Type Type { get; set; } = typeof(string);
    public bool IsOptional { get; set; }
    public object? DefaultValue { get; set; }
}

public class ToolInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public MethodInfo Method { get; set; } = null!;
    public List<ToolParameter> Parameters { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";

    public ChatMessage() { }
    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}
