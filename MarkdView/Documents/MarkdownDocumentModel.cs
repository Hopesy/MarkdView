using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MarkdView.Documents;

/// <summary>
/// 不暴露解析器类型的 Markdown 文档快照。
/// 快照只保留稳定的文档语义和原文，不持有 Markdig AST，避免把解析器对象图
/// 带入缓存、渲染器和其他下游边界。
/// </summary>
public sealed class MarkdownDocumentModel
{
    /// <summary>
    /// 创建一个稳定的 Markdown 文档快照。
    /// 自定义 <see cref="MarkdView.Parsing.IMarkdownDocumentParser"/> 可以使用此构造函数返回自己的模型，
    /// 顶层块集合会被复制，避免调用方后续修改集合而影响已发布的快照。
    /// </summary>
    public MarkdownDocumentModel(
        string sourceText,
        IReadOnlyList<MarkdownBlockModel> blocks)
    {
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        ArgumentNullException.ThrowIfNull(blocks);
        Blocks = new ReadOnlyCollection<MarkdownBlockModel>(blocks.ToArray());
        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceText)));
    }

    public string SourceText { get; }

    public string ContentHash { get; }

    public IReadOnlyList<MarkdownBlockModel> Blocks { get; }
}

public enum MarkdownBlockKind
{
    Unknown = 0,
    Heading,
    Paragraph,
    Quote,
    List,
    ListItem,
    Table,
    TableRow,
    TableCell,
    Code,
    ThematicBreak,
    Html,
    DefinitionList,
    DefinitionItem,
    DefinitionTerm,
    FootnoteGroup,
    Footnote,
    Math
}

public enum MarkdownTableColumnAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// 一个块级节点的稳定描述，包含原文范围，便于增量更新和诊断。
/// </summary>
public sealed record MarkdownBlockModel(
    MarkdownBlockKind Kind,
    string SourceText,
    int Start,
    int Length,
    int HeadingLevel,
    string? Language,
    bool IsOrdered,
    int OrderedStart,
    IReadOnlyList<MarkdownBlockModel> Children)
{
    /// <summary>
    /// 当前块的直接内联节点。列表、引用和表格等容器块为空，子块承载实际内联内容。
    /// </summary>
    public IReadOnlyList<MarkdownInlineModel> Inlines { get; init; } = Array.Empty<MarkdownInlineModel>();

    /// <summary>
    /// 代码块的原始代码内容，不包含 fenced 标记。
    /// </summary>
    public string? CodeText { get; init; }

    public bool IsTableHeader { get; init; }

    public int ColumnIndex { get; init; } = -1;

    public int ColumnSpan { get; init; } = 1;

    public int RowSpan { get; init; } = 1;

    /// <summary>
    /// 表格块的列对齐元数据；非表格块为空集合，未指定对齐的列为 null。
    /// </summary>
    public IReadOnlyList<MarkdownTableColumnAlignment?> TableColumnAlignments { get; init; }
        = Array.Empty<MarkdownTableColumnAlignment?>();

    /// <summary>
    /// 解析器节点的稳定诊断名称。渲染层不依赖该名称做转换，但可据此记录兼容回退原因。
    /// </summary>
    public string SyntaxType { get; init; } = string.Empty;

    public bool RequiresCompatibilityRenderer { get; init; }
}

public enum MarkdownInlineKind
{
    Unknown = 0,
    Text,
    Emphasis,
    Strong,
    Strikethrough,
    Code,
    LineBreak,
    Autolink,
    Link,
    Image,
    Html,
    Task,
    Math,
    FootnoteLink
}

/// <summary>
/// 一个内联节点的稳定描述。链接地址、任务状态和强调语义均以数据形式保存，渲染层不必依赖解析器类型。
/// </summary>
public sealed record MarkdownInlineModel(
    MarkdownInlineKind Kind,
    string SourceText,
    int Start,
    int Length,
    string? Text,
    string? Url,
    string? Title,
    bool IsEmail,
    bool IsImage,
    bool? IsChecked,
    char DelimiterChar,
    int DelimiterCount,
    IReadOnlyList<MarkdownInlineModel> Children)
{
    /// <summary>
    /// 解析器内联节点的稳定诊断名称。
    /// </summary>
    public string SyntaxType { get; init; } = string.Empty;

    public bool RequiresCompatibilityRenderer { get; init; }
}
