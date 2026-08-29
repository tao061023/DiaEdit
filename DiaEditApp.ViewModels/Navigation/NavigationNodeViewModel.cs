namespace DiaEditApp.ViewModels.Navigation;

using System;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// ナビゲーションツリー（UI設計書4.1節）の1ノードを表す軽量ViewModel。
///
/// M2-1時点では「駅／駅一覧」のみが実ノード（ContentFactoryを持つリーフ）で、
/// 他カテゴリ（路線／車両／時刻表／お気に入り）は空のフォルダノード
/// （IsLeaf=false・ContentFactory=null・Children=空）として先行実装する。
///
/// ContentFactoryはFunc&lt;IServiceProvider, ViewModelBase&gt;とし、選択時に
/// MainViewModel側がDIコンテナから解決する（画面ViewModelはTransient登録のため、
/// ノード選択のたびに新規インスタンスが生成される想定。§7.3 論点L：Dispose規約に従う）。
/// </summary>
public sealed partial class NavigationNodeViewModel : ObservableObject
{
    public string Header { get; }

    public bool IsLeaf { get; }

    public ObservableCollection<NavigationNodeViewModel> Children { get; } = new();

    public Func<IServiceProvider, ViewModelBase>? ContentFactory { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    /// <summary>フォルダノード（子を持つが自身はコンテンツを持たない）を作る。</summary>
    public static NavigationNodeViewModel Folder(string header, params NavigationNodeViewModel[] children)
    {
        var node = new NavigationNodeViewModel(header, isLeaf: false, contentFactory: null);
        foreach (var child in children)
        {
            node.Children.Add(child);
        }
        return node;
    }

    /// <summary>リーフノード（クリックでメインワークスペースにコンテンツを表示する）を作る。</summary>
    public static NavigationNodeViewModel Leaf(string header, Func<IServiceProvider, ViewModelBase> contentFactory)
        => new(header, isLeaf: true, contentFactory: contentFactory);

    private NavigationNodeViewModel(string header, bool isLeaf, Func<IServiceProvider, ViewModelBase>? contentFactory)
    {
        Header = header;
        IsLeaf = isLeaf;
        ContentFactory = contentFactory;
    }
}