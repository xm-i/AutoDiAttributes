# AutoDiAttributes

`RegisterServiceAttribute` を付与したクラスを自動的に `IServiceCollection` へ追加する C# Source Generator。

- .NET 10 を対象
- `IIncrementalGenerator` を採用した高速・差分ビルド対応
- クラス単位で DI 登録 (Transient / Scoped / Singleton)
- サービス型を明示しない場合は実装型 = サービス型として登録

## 使い方 (プロジェクト内で直接参照する場合)
1. `AutoDiAttributes` と `AutoDiAttributes.Generator` プロジェクトをソリューションに含めるか、NuGet パッケージ化して参照します。
2. 対象クラスに `RegisterServiceAttribute` を付与します。
3. ビルドすると `GeneratedDI.DIRegistration` クラスが生成されます。
4. アプリ起動時に `services.AddGeneratedServices()` を呼び出します。

```csharp
using AutoDiAttributes;

[RegisterService(ServiceLifetime.Scoped, typeof(IMyService))]
public class MyService : IMyService
{
    // 実装
}

[RegisterService(ServiceLifetime.Singleton)]
public class ClockProvider : IClockProvider
{
    // サービス型を省略したので ClockProvider 自身がサービスとして登録される
}
```

起動時:
```csharp
using GeneratedDI; // 生成された namespace

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGeneratedServices();
```

## 属性の引数
```csharp
RegisterService(ServiceLifetime lifetime)
RegisterService(ServiceLifetime lifetime, Type serviceType)
```

- `lifetime`: `Transient`, `Scoped`, `Singleton`
- `serviceType`: 登録時に使うサービスインターフェイス/抽象型。省略時は実装型。

## 生成されるコード概要
ビルド時に全ソースを走査し、`RegisterServiceAttribute` が付いたクラスを抽出。以下のようなコードを生成します。
```csharp
using Microsoft.Extensions.DependencyInjection;
namespace GeneratedDI;
public static class DIRegistration
{
    public static void AddGeneratedServices(IServiceCollection services)
    {
        // services.AddScoped<IMyService, MyService>(); 等が展開される
    }
}
```

## 注意点 / 制限
- 同じクラスに複数の `RegisterServiceAttribute` はサポートしていません (複数登録が必要なら別クラス/Adapter を用意してください)
- ジェネリック型の特殊化ごとの登録は未対応

## トラブルシュート
| 症状 | 原因 | 対処 |
|------|------|------|
| 生成クラスが見えない | 参照/ビルド前の IntelliSense キャッシュ | 一度ビルド、`obj/Generated` の生成物確認 |
| 期待する登録が無い | 属性の名前空間が異なる/typo | `using AutoDiAttributes;` を追加、属性名を確認 |
| Lifetime が期待と違う | 引数順序違い | `(ServiceLifetime, Type)` 順で指定 |