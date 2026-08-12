namespace DiaEditCore.Composition;

using Microsoft.Extensions.DependencyInjection;

using DiaEditCore.Commands;
using DiaEditCore.Session;

/// <summary>
/// DiaEditCore側のDIコンテナ登録（7.3節・8.2節旧項目9）。
///
/// 論点J（v11.39確定）：IValidator&lt;T&gt;実装（StationValidator等22個）は、アセンブリスキャンによる
/// 自動登録ではなく明示的登録の方針とした。ただし現時点ではSaveValidationRunnerがそれぞれを
/// `new`で直接インスタンス化しており（状態を持たないためDI経由で共有する必要が無い）、
/// DIコンテナ経由でIValidator&lt;T&gt;を解決する具体的な利用箇所が無い。そのため、本メソッドでは
/// Validatorの登録は行わない。ViewModel層等がValidatorをDI経由で必要とする具体的な用途が
/// 出てきた時点で、SaveValidationRunner.csの列挙をそのままこちらへ複製する形で追加する
/// （§8.2項目9の教訓：紛らわしい命名の実装を誤登録しないよう、コピー元を1箇所に保つ）。
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddDiaEditCore(this IServiceCollection services)
    {
        // CommandInvokerはUndo/Redo履歴・ICacheChangeObserver購読者一覧をアプリ全体で共有するため、
        // Singletonとして登録する（論点K）。
        services.AddSingleton<CommandInvoker>();
        services.AddSingleton<ProjectSession>(); 

        return services;
    }
}
