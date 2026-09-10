namespace DiaEditCore.Model;

/// <summary>
/// 座標点を保持する共通基底型
/// </summary>
/// <remarks>
/// intである理由は座標をグリッドで管理することが共通事項であるため。
/// </remarks>
/// <param name="X">X座標</param>
/// <param name="Y">Y座標</param>
public readonly record struct Point(int X, int Y);