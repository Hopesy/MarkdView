using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkdView;
using MarkdView.Enums;
using Samples.Models;

namespace Samples.ViewModels;

/// <summary>
/// Markdown 列表 ViewModel
/// </summary>
public partial class MarkdownListViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<MarkdownItem> _items = new();

    [ObservableProperty]
    private bool _isEmpty = true;

    public MarkdownListViewModel()
    {
        Items.CollectionChanged += (s, e) => IsEmpty = Items.Count == 0;
    }

    /// <summary>
    /// 添加项目
    /// </summary>
    public void AddItem(MarkdownItem item)
    {
        Items.Add(item);
    }

    /// <summary>
    /// 清空列表
    /// </summary>
    public void ClearItems()
    {
        Items.Clear();
    }

    /// <summary>
    /// 切换到浅色主题 - 直接调用全局 ThemeManager
    /// </summary>
    [RelayCommand]
    private void SwitchToLightTheme()
    {
        ThemeManager.ApplyTheme(ThemeMode.Light);
    }

    /// <summary>
    /// 切换到深色主题 - 直接调用全局 ThemeManager
    /// </summary>
    [RelayCommand]
    private void SwitchToDarkTheme()
    {
        ThemeManager.ApplyTheme(ThemeMode.Dark);
    }
}


