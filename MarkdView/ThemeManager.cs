using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using MarkdView.Enums;

namespace MarkdView;

/// <summary>
/// 主题管理器 - 负责通过切换资源字典来管理全局主题
/// </summary>
public static class ThemeManager
{
    private const string LightThemeUri = "pack://application:,,,/MarkdView;component/Themes/MarkdView.Light.xaml";
    private const string DarkThemeUri = "pack://application:,,,/MarkdView;component/Themes/MarkdView.Dark.xaml";

    private static ThemeMode _currentTheme = ThemeMode.Dark; // 默认深色主题
    private static readonly object _themeLock = new object();

    /// <summary>
    /// 全局当前主题 - 所有主题变更的唯一真实来源
    /// </summary>
    public static ThemeMode CurrentTheme
    {
        get
        {
            lock (_themeLock)
            {
                return _currentTheme;
            }
        }
        private set
        {
            lock (_themeLock)
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    System.Diagnostics.Debug.WriteLine($"[ThemeManager] CurrentTheme changed to: {value}");
                }
            }
        }
    }

    /// <summary>
    /// 主题应用完成事件 - 当主题资源字典被替换后触发
    /// </summary>
    public static event EventHandler? ThemeApplied;

    /// <summary>
    /// 获取当前应用的主题（通过检查已加载的资源字典）
    /// </summary>
    public static ThemeMode GetCurrentTheme()
    {
        var app = Application.Current;
        if (app == null)
        {
            return CurrentTheme;
        }

        if (!app.Dispatcher.CheckAccess())
        {
            if (IsDispatcherTerminated(app.Dispatcher))
            {
                // Dispatcher 已经关闭时不能从后台线程访问应用资源；保留现有主题。
                return CurrentTheme;
            }

            if (app.Dispatcher.Thread.IsAlive)
            {
                return app.Dispatcher.Invoke(() => GetCurrentThemeOnUiThread(app));
            }

            // 兼容宿主在旧 STA 线程创建 Application 后退出线程的场景。
            // 此时没有可等待的 Dispatcher，直接读取静态资源比 Invoke 更安全。
            return GetCurrentThemeOnUiThread(app);
        }

        return GetCurrentThemeOnUiThread(app);
    }

    private static ThemeMode GetCurrentThemeOnUiThread(Application app)
    {
        var existingTheme = app.Resources.MergedDictionaries
            // MergedDictionaries 后加入的字典具有更高优先级；重复主题时应以实际生效的最后一项为准。
            .LastOrDefault(d => IsMarkdViewTheme(d.Source));

        if (existingTheme?.Source?.ToString().Contains("MarkdView.Light.xaml", StringComparison.OrdinalIgnoreCase) == true)
        {
            CurrentTheme = ThemeMode.Light;
            return ThemeMode.Light;
        }

        if (existingTheme?.Source?.ToString().Contains("MarkdView.Dark.xaml", StringComparison.OrdinalIgnoreCase) == true)
        {
            CurrentTheme = ThemeMode.Dark;
            return ThemeMode.Dark;
        }

        return CurrentTheme;
    }

    /// <summary>
    /// 应用指定主题
    /// </summary>
    /// <param name="theme">要应用的主题</param>
    public static void ApplyTheme(ThemeMode theme)
    {
        if (theme is not (ThemeMode.Auto or ThemeMode.Light or ThemeMode.Dark))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "未知主题模式");
        }

        var app = Application.Current;
        if (app == null)
        {
            CurrentTheme = theme == ThemeMode.Auto ? CurrentTheme : theme;
            return;
        }

        // ResourceDictionary 和依赖对象具有 Dispatcher 线程亲和性。
        if (!app.Dispatcher.CheckAccess())
        {
            if (IsDispatcherTerminated(app.Dispatcher))
            {
                throw new InvalidOperationException("应用 Dispatcher 已关闭，无法应用主题。");
            }

            if (app.Dispatcher.Thread.IsAlive)
            {
                try
                {
                    app.Dispatcher.Invoke(() => ApplyThemeOnUiThread(app, theme));
                }
                catch (InvalidOperationException) when (IsDispatcherTerminated(app.Dispatcher))
                {
                    // Dispatcher 可能在检查和 Invoke 之间进入关闭流程；旧主题保持不变。
                    System.Diagnostics.Debug.WriteLine("[ThemeManager] 应用 Dispatcher 在主题切换期间关闭。");
                }
            }
            else
            {
                // 兼容无窗口宿主的孤立 Application；已终止的 Dispatcher 在上面直接拒绝。
                ApplyThemeOnUiThread(app, theme);
            }

            return;
        }

        ApplyThemeOnUiThread(app, theme);
    }

    private static void ApplyThemeOnUiThread(Application app, ThemeMode requestedTheme)
    {
        // Auto 应先读取宿主已安装的 MarkdView 字典，避免静态默认值覆盖宿主启动时预加载的主题。
        var theme = requestedTheme == ThemeMode.Auto
            ? GetCurrentThemeOnUiThread(app)
            : requestedTheme;
        var themeUri = theme == ThemeMode.Light ? LightThemeUri : DarkThemeUri;

        // 相同主题已经安装时无需重新替换资源字典或触发整棵控件树重渲染。
        var installedThemes = app.Resources.MergedDictionaries
            .Where(d => IsMarkdViewTheme(d.Source))
            .ToArray();
        if (CurrentTheme == theme
            && installedThemes.Length == 1
            && installedThemes[0].Source?.ToString().Contains(
                theme == ThemeMode.Light ? "MarkdView.Light.xaml" : "MarkdView.Dark.xaml",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return;
        }

        ApplyThemeFromUri(app, themeUri, theme);
    }

    /// <summary>
    /// 确保控件第一次加载时有一个可用的 MarkdView 主题资源字典。
    /// </summary>
    public static void EnsureThemeApplied()
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        if (!app.Dispatcher.CheckAccess())
        {
            if (IsDispatcherTerminated(app.Dispatcher))
            {
                return;
            }

            if (app.Dispatcher.Thread.IsAlive)
            {
                try
                {
                    app.Dispatcher.Invoke(() => EnsureThemeAppliedOnUiThread(app));
                }
                catch (InvalidOperationException) when (IsDispatcherTerminated(app.Dispatcher))
                {
                    System.Diagnostics.Debug.WriteLine("[ThemeManager] 应用 Dispatcher 在确保主题期间关闭。");
                }
            }
            else
            {
                EnsureThemeAppliedOnUiThread(app);
            }

            return;
        }

        EnsureThemeAppliedOnUiThread(app);
    }

    private static void EnsureThemeAppliedOnUiThread(Application app)
    {
        var installedThemes = app.Resources.MergedDictionaries
            .Where(d => IsMarkdViewTheme(d.Source))
            .ToArray();

        if (installedThemes.Length == 0)
        {
            ApplyTheme(CurrentTheme);
            return;
        }

        // 宿主可能在启动前重复合并主题字典；整理为单一字典，避免资源解析和通知出现不确定性。
        if (installedThemes.Length > 1)
        {
            ApplyTheme(GetCurrentThemeOnUiThread(app));
        }
    }

    /// <summary>
    /// 从 URI 加载主题资源字典
    /// </summary>
    private static void ApplyThemeFromUri(Application app, string themeUri, ThemeMode theme)
    {
        try
        {
            var uri = new Uri(themeUri, UriKind.Absolute);
            var newTheme = new ResourceDictionary { Source = uri };

            // 先完成加载，再替换旧字典。这样加载失败不会破坏当前主题。
            var oldThemes = app.Resources.MergedDictionaries
                .Where(d => IsMarkdViewTheme(d.Source))
                .ToArray();
            var dictionaries = app.Resources.MergedDictionaries;
            var insertIndex = oldThemes.Length > 0
                ? dictionaries.IndexOf(oldThemes[0])
                : dictionaries.Count;
            if (insertIndex < 0 || insertIndex > dictionaries.Count)
            {
                insertIndex = dictionaries.Count;
            }

            // 在旧主题的原位置替换，保留宿主自定义字典相对于 MarkdView 的优先级。
            dictionaries.Insert(insertIndex, newTheme);
            foreach (var oldTheme in oldThemes)
            {
                dictionaries.Remove(oldTheme);
            }

            CurrentTheme = theme;

            // 订阅者异常不能回滚已完成的资源替换。
            foreach (EventHandler handler in ThemeApplied?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try { handler(app, EventArgs.Empty); }
                catch (Exception subscriberException)
                {
                    System.Diagnostics.Debug.WriteLine($"[ThemeManager] ThemeApplied handler failed: {subscriberException}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeManager] 加载主题失败: {ex}");
        }
    }

    private static bool IsMarkdViewTheme(Uri? source)
    {
        var sourceText = source?.ToString();
        return sourceText?.Contains("MarkdView.Light.xaml", StringComparison.OrdinalIgnoreCase) == true
            || sourceText?.Contains("MarkdView.Dark.xaml", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsDispatcherTerminated(Dispatcher dispatcher)
        => dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished;

}
