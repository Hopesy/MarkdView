using Markdig;

namespace MarkdView.Parsing;

/// <summary>
/// MarkdView 默认 Markdown 能力的唯一 pipeline 配置入口。
/// 宿主仍可通过 <see cref="MarkdView.Renderers.MarkdownRenderer"/> 构造函数注入自定义 pipeline。
/// </summary>
public static class MarkdownPipelineFactory
{
    public static MarkdownPipeline CreateDefault()
        => new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseDefinitionLists()
            .UseFootnotes()
            .UseMathematics()
            .UseEmojiAndSmiley()
            .UseTaskLists()
            .UseMediaLinks()
            .Build();
}
