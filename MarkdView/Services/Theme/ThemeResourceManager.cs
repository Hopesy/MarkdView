using System.Windows;
using System.Windows.Media;

namespace MarkdView.Services.Theme;

/// <summary>
/// 主题资源管理器 - 统一管理主题相关的资源获取
/// 支持动态主题切换
/// </summary>
public class ThemeResourceManager
{
    private readonly FrameworkElement _element;

    public ThemeResourceManager(FrameworkElement element)
    {
        _element = element;
    }

    /// <summary>
    /// 为元素设置动态资源引用（主题切换时自动更新）
    /// </summary>
    public void SetDynamicResource(FrameworkElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        // 先确保资源字典中有这个键（如果没有就添加默认值）
        if (!Application.Current.Resources.Contains(resourceKey))
        {
            Application.Current.Resources[resourceKey] = defaultValue;
        }

        // 总是建立动态绑定
        element.SetResourceReference(property, resourceKey);
    }

    /// <summary>
    /// 为 FrameworkContentElement 设置动态资源引用（用于 FlowDocument 等元素）
    /// </summary>
    public void SetDynamicResource(FrameworkContentElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        // 先确保资源字典中有这个键（如果没有就添加默认值）
        if (!Application.Current.Resources.Contains(resourceKey))
        {
            Application.Current.Resources[resourceKey] = defaultValue;
        }

        // 总是建立动态绑定
        element.SetResourceReference(property, resourceKey);
    }

    /// <summary>
    /// 为 TextElement 设置动态资源引用（用于 FlowDocument 内部元素）
    /// </summary>
    public void SetDynamicResource(System.Windows.Documents.TextElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        // 先确保资源字典中有这个键（如果没有就添加默认值）
        if (!Application.Current.Resources.Contains(resourceKey))
        {
            Application.Current.Resources[resourceKey] = defaultValue;
        }

        // 总是建立动态绑定
        element.SetResourceReference(property, resourceKey);
    }

    /// <summary>
    /// 获取画刷资源（静态获取，用于不支持 DynamicResource 的场景）
    /// </summary>
    public Brush GetBrush(string key, Color defaultColor)
    {
        if (_element.TryFindResource(key) is Brush brush)
        {
            return brush;
        }
        return new SolidColorBrush(defaultColor);
    }

    /// <summary>
    /// 获取字体家族
    /// </summary>
    public FontFamily GetFontFamily(string key, string defaultFont)
    {
        if (_element.TryFindResource(key) is FontFamily font)
        {
            return font;
        }
        return new FontFamily(defaultFont);
    }

    /// <summary>
    /// 获取字体大小
    /// </summary>
    public double GetFontSize(string key, double defaultSize)
    {
        if (_element.TryFindResource(key) is double size)
        {
            return size;
        }
        return defaultSize;
    }

    /// <summary>
    /// 获取双精度值
    /// </summary>
    public double GetDouble(string key, double defaultValue)
    {
        if (_element.TryFindResource(key) is double value)
        {
            return value;
        }
        return defaultValue;
    }

    /// <summary>
    /// 获取厚度
    /// </summary>
    public Thickness GetThickness(string key, Thickness defaultThickness)
    {
        if (_element.TryFindResource(key) is Thickness thickness)
        {
            return thickness;
        }
        return defaultThickness;
    }

    /// <summary>
    /// 获取圆角半径
    /// </summary>
    public CornerRadius GetCornerRadius(string key, CornerRadius defaultCornerRadius)
    {
        if (_element.TryFindResource(key) is CornerRadius radius)
        {
            return radius;
        }
        return defaultCornerRadius;
    }
}
