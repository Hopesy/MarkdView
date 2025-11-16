### 前言

> 本案例简单介绍**MCP 官方** `C# SDK`的案例，**实现`stdio/SSE`传输的 MCP 服务，并创建 MCP 客户端使用 LLM 调用 MCP 服务。**

- 项目地址：[https://gitee.com/hopesy/saury-mcp](https://gitee.com/hopesy/saury-mcp)

- 消息要符合`JSON-RPC` 2.0 规范，标准输入输出流/SSE

- MCP 协议相当于已经把前端和后端的 API 定义好了，各自去实现就行了。

- 客户端需要实现 MCP 协议，各种 MCP 服务也要实现 MCP 协议，两者才能进行通信。

![](https://s2.loli.net/2025/04/20/onhCevsZ6bEtLTj.png)

![](https://s2.loli.net/2025/04/20/wliXWAHER6uGeO1.png)

> MCP 官方项目中有许多.Net SDK 的案例以及详细的入门指南。[https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples](https://github.com/modelcontextprotocol/csharp-sdk/tree/main/samples)

![](https://s2.loli.net/2025/04/20/EIqe6g5RoFh93CV.png)

![](https://s2.loli.net/2025/04/20/VFDAdCa3jhEH15U.png)

### MCP 协议

> 模型上下文协议（MCP）是一种开放协议，它规范了应用程序如何向大型语言模型（LLM）提供上下文。它能够实现大型语言模型与各种数据源和工具之间的安全集成。MCP 的核心组件包括：

- **主机（Host）**：运行 LLM 的应用程序（如 Claude Desktop、Cursor、Cline、Windsurf），负责启动客户端并发起与 MCP 服务器的连接。

- **客户端（Client）**：在主机应用程序内部运行，与 MCP 服务器建立 1:1 连接。

- **服务器（Server）**：提供对外部数据源和工具的访问，响应客户端的请求。

- **LLM**：大型语言模型，通过 MCP 获取上下文并生成输出。

- **工作流程**：主机启动客户端 → 客户端连接到 MCP 服务器 → 服务器提供资源、提示或工具 →LLM 使用这些信息生成响应

![](https://s2.loli.net/2025/04/20/RhyKO4g96m7JFkS.png)

1. 用户发送可能需要访问该函数的提示，例如：“北京的当前天气如何？”

2. **Agent 将提示与所有可用函数一起发送**。在我们的示例中，这可能是提示以及函数 get_current_weather(city) 的输入模式。LLM 确定提示是否需要函数调用。如果是，**LLM 会查找提供的函数列表**及其各自的模式并使用包含有函数集及其**输入参数**的 JSON 字典进行响应。

3. **Agent 解析 LLM 响应。如果它包含函数，它将按顺序或并行调用它们**。

4. 然后将**每个函数的输出包含在最终提示中并发送到 LLM**。由于模型现在可以访问数据，因此它会根据函数提供的事实数据做出回答。

> 在 MCP（Model Context Protocol）中，为客户端-服务器通信定义了两种默认标准传输机制：

- `stdio`（标准输入输出） ：适用于客户端与服务端在同一台机器上的场景。客户端通过启动服务端子进程（如命令行工具），利用操作系统的**管道机制**（stdin/stdout）进行数据传输。是个**同步阻塞模型**，通信基于顺序处理，需等待前一条消息完成传输后才能处理下一条，**适合简单的本地批处理任务**。

- `HTTP with SSE`（Server-Sent Events） 适用于客户端与服务端部署在不同节点，通过**HTTP 协议**实现跨网络通信。服务端通过 SSE 长连接主动推送数据，客户端通过 HTTP POST 端点发送请求。是个**异步事件驱动**，支持实时或准实时交互，**适合分布式系统或需要高并发的场景**。

- 自定义：MCP 允许开发者实现其他传输方式（如 WebSocket、gRPC），但需满足**`JSON-RPC 2.0`的消息格式**和生命周期管理。如需实现双向实时通信（如游戏或双向聊天），就需要基于 MCP 自定义其他传输（如 WebSocket）。

![](https://s2.loli.net/2025/04/20/kn7OtzWfaHhN9F6.png)

### 案例

> **MCP Server（Stdio）**：创建控制台项目`Console`→ 添加 MCP 官方的包`ModelContextProtocol`使用标准的 IO 传输方式/SSE→ 创建工具类`McpServerToolType`特性修饰，添加工具方法使用特性`McpServerTool`修饰 → 修改`Program.cs`添加工具并设置为启动`MCP Server`

![](https://s2.loli.net/2025/04/20/5jwZkJ1yreRzbIh.png)

```C#
//【1】创建控制台项目，并添加MCP官方包
dotnet add package ModelContextProtocol --prerelease
dotnet add package Microsoft.Extensions.Hosting
//【2】使用特性定义MCP Tool
[McpServerToolType]
public static class TimeTool
{
    [McpServerTool, Description("获取指定城市的时间")]
    public static string GetCurrentTime(string city) =>
        $"It is {DateTime.Now.Hour}:{DateTime.Now.Minute} in {city}.";
}
//【3】在入口文件添加定义的Tool
//CreateEmptyApplicationBuilder创建一个最小化的应用程序构建器，它不包含任何默认配置
Console.WriteLine("Starting MCP Server...");
var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()//标准IO传输方式
    .WithToolsFromAssembly();//扫描程序集中添加了McpServerTool特性的类进行注册
    // .WithTools<EchoTool>()//单个添加
await builder.Build().RunAsync();
```

![](https://s2.loli.net/2025/04/20/jzE3LsBMYT6mISr.png)

> **MCP Server（SSE）**：创建`ASP.NET Core API`项目 → 添加 MCP 官方的包`ModelContextProtocol`→ 创建工具类`McpServerToolType`特性修饰，添加工具方法使用特性`McpServerTool`修饰 → 修改`Program.cs`添加工具并设置为启动`MCP Server`

![](https://s2.loli.net/2025/04/20/DhgHTc713ojqMr4.png)

```C#
//【1】创建ASP.NET Core API项目，并添加MCP官方包
dotnet add package ModelContextProtocol --prerelease
dotnet add package ModelContextProtocol --prerelease
dotnet add package Microsoft.Extensions.Hosting
//【2】使用特性定义MCP Tool
[McpServerToolType]
public sealed class EchoTool
{
    [McpServerTool, Description("向MCP Client输出示例信息.")]
    public static string Echo(string message)
    {
        return "hello " + message;
    }
}
//【3】在入口文件添加定义的Tool
var builder = WebApplication.CreateBuilder(args);
//HTTP SSE传输；注册MCP Tool
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<EchoTool>()
    .WithTools<TimeTool>();
var app = builder.Build();
app.MapMcp();
app.Run();
```

![](https://s2.loli.net/2025/04/20/cjBqLIO4nZV1y8X.png)

> **MCP Client**：创建`ASP.NET Core API`项目 → 添加 MCP 官方的包`ModelContextProtocol`→ 配置`MCP 服务路径`→ 配置 LLM 大模型 API→ 实现对话

```C#
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.4.0-preview.1.25207.5" />
<!--除了OpenAI及其兼容的AI，也可以添加Ollama包使用本地部署的模型-->
<PackageReference Include="ModelContextProtocol" Version="0.1.0-preview.9" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
```

```C#
{
  "LLM": {
    "EndPoint": "https://api.siliconflow.cn",
    "ApiKey": "*********************************",
    "ModelId": "Qwen/Qwen2.5-7B-Instruct"
  }
}
```

```C#
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json");
builder.Logging.AddConsole();
var host = builder.Build();
//【1】加载MCP Server(SSE)
//var clientSse = new SseClientTransport(new()
//{
//    Name = "Demo Sse Server",
//    Endpoint = new Uri("http://localhost:5066/sse"),
//    AdditionalHeaders = []
//});
//【2】加载MCP Server(Stdio)
var clientTransport = new StdioClientTransport(new()
{
    Name = "Demo Std Server",
    //Command = "uvx",
    //Arguments = ["mcp-server-fetch",]
    Command = "cmd",
    Arguments = ["/C",
        "C:\\Users\\zhouh\\RiderProjects\\SauryMCP\\Saury.McpServer\\bin\\Debug\\net8.0\\server.exe"],
    EnvironmentVariables = [],
});
Console.WriteLine("MCP Client Started!");
//【3】创建MCP Client
await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport);
var mcpTools = await mcpClient.ListToolsAsync();
foreach (var tool in mcpTools)
{   //列出所有加载的服务中的所有方法
    Console.WriteLine($"工具：{tool.Name}\n描述：{tool.Description}");
}
//【4】模拟直接调用MCP Tool(仅测试，一般是LLM自主调用)
// var result = await mcpClient.CallToolAsync(
//     "GetCurrentTime",
//     new Dictionary<string, object?>() { ["city"] = "Chengdu" }
//     );
// Console.WriteLine(result.Content.First(c => c.Type == "text").Text);
//【5】LLM大模型对话调用MCP Tool
// 获取配置，通过层级路径获取配置值
var config = host.Services.GetRequiredService<IConfiguration>();
// 通过配置文件获取大模型配置，本次使用了硅基流动的API
// 实测Qwen模型对MCP服务能够准确解析
string apiKey = config["LLM:ApiKey"];
var apiKeyCredential = new ApiKeyCredential(apiKey );
var aiClientOptions = new OpenAIClientOptions();
aiClientOptions.Endpoint = new Uri(config["LLM:EndPoint"]);
var aiClient = new OpenAIClient(apiKeyCredential, aiClientOptions)
    .AsChatClient(config["LLM:ModelId"]);
var chatClient = new ChatClientBuilder(aiClient)
    .UseFunctionInvocation()
    .Build();
//系统提示词System,Assistant,User
IList<ChatMessage> chatHistory =
[
    new(ChatRole.System, """你是一个智能助手，能加载MCP服务，为用户提供城市时间查询等"""),
];
var chatOptions = new ChatOptions()
{
    Tools = [.. mcpTools]
};
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Assistant> 我该怎样帮助你?");
while (true)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write("User> ");
    var question = Console.ReadLine();
    // 用户输入EXIT或者空格后按下Enter会退出循环，进而退出程序
    if (!string.IsNullOrWhiteSpace(question) && question.ToUpper() == "EXIT")
        break;
    chatHistory.Add(new ChatMessage(ChatRole.User, question));
    Console.ForegroundColor = ConsoleColor.Green;
    // 向AI提问的时候，附加上MCP的工具，启用FunCall功能后AI会自主调用
    var response = await chatClient.GetResponseAsync(chatHistory, chatOptions);
    var content = response.ToString();
    Console.WriteLine($"Assistant> {content}");
    chatHistory.Add(new ChatMessage(ChatRole.Assistant, content));
    Console.WriteLine();
}
await host.RunAsync();
```

### 调试

> `MCP Inspector` 是专为 Model Context Protocol（MCP）服务器设计的**交互式调试工具**，支持开发者通过多种方式快速测试与优化服务端功能。

比如你是通过 `node build/index.js` 执行 MCP 服务器端的命令，则可以用下面方式启动调试

```Shell
#测试自己的本地项目
npx @modelcontextprotocol/inspector node build/index.js
#测试已经发布的其他服务
npx @modelcontextprotocol/inspector uvx mcp-server-fetch
```

如果 MCP 服务器启动需要参数和环境变量，也可以通过下面方式传递：

```Shell
# 传递参数 arg1 arg2
npx @modelcontextprotocol/inspector node build/index.js arg1 arg2
# 传递环境变量 KEY=value  KEY2=$VALUE2
npx @modelcontextprotocol/inspector -e KEY=value -e KEY2=$VALUE2 node build/index.js
# 同时传递环境变量和参数
npx @modelcontextprotocol/inspector -e KEY=value -e KEY2=$VALUE2 node build/index.js arg1 arg2
# Use -- to separate inspector flags from server arguments
npx @modelcontextprotocol/inspector -e KEY=$VALUE -- node build/index.js -e server-flag
```

![](https://s2.loli.net/2025/04/20/JrRh1dmKqcAiD74.png)

![](https://s2.loli.net/2025/04/20/ROH1AjKTkSbPsic.png)

也可以启动 inspector 后配置**SSE 服务地址**进行连接测试

![](https://s2.loli.net/2025/04/20/vThJjqGEw4mZs3f.png)
