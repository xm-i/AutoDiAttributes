# AutoDiAttributes

`InjectAttribute` を付与したクラスを自動的に `IServiceCollection` へ追加する C# Source Generator です。

- .NET Standard 2.0 準拠
- `IIncrementalGenerator` を採用した高速・差分ビルド対応
- クラス単位で DI 登録 (Transient / Scoped / Singleton)
- サービス型を明示しない場合は実装型をサービス型として登録

## 使い方

1. `AutoDiAttributes` と `AutoDiAttributes.Generator` プロジェクトをソリューションに含めるか、パッケージ参照を追加します。
2. 対象のクラスに `InjectAttribute` を付与します。
3. ビルドすると、参照元のアセンブリ名に基づいた名前空間に `DIRegistration` クラスが生成されます。
4. アプリケーションの起動時に `services.AddGeneratedServices()` を呼び出して登録を行います。

```csharp
using AutoDiAttributes;

[Inject(InjectServiceLifetime.Scoped, typeof(IMyService))]
public class MyService : IMyService
{
    // 実装
}

[Inject(InjectServiceLifetime.Singleton)]
public class ClockProvider : IClockProvider
{
    // サービス型を省略したため、ClockProvider 自身がサービスとして登録されます
}
```

起動時の登録例:

```csharp
using Microsoft.Extensions.DependencyInjection;
using 参照元のアセンブリ名; // アセンブリ名に基づき生成された名前空間

var builder = WebApplication.CreateBuilder(args);

// 拡張メソッドとして呼び出します
builder.Services.AddGeneratedServices();
```

## 属性のコンストラクタ

```csharp
InjectAttribute(InjectServiceLifetime lifetime)
InjectAttribute(InjectServiceLifetime lifetime, Type serviceType)
```

- `lifetime`: `InjectServiceLifetime.Transient`, `InjectServiceLifetime.Scoped`, `InjectServiceLifetime.Singleton` のいずれかを指定します。
- `serviceType`: 登録時に使用するサービスのインターフェースや抽象型を指定します。省略した場合は実装型がそのまま登録されます。

## 生成されるコードの概要

ビルド時にソースコードを走査し、`InjectAttribute` が付与されたクラスを抽出して以下のようなコードを生成します。

```csharp
namespace アセンブリ名に基づいた名前空間;

public static class DIRegistration
{
    public static void AddGeneratedServices(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<IMyService, MyService>(services);
        // その他の登録処理が展開されます
    }
}
```

## 制限事項

- ジェネリック型の特殊化ごとの自動登録は現在未対応です。

## トラブルシューティング

| 症状 | 原因 | 対処方法 |
|------|------|----------|
| 生成されたクラスが見つからない | 参照設定の不足、または IntelliSense のキャッシュ | 一度ビルドを実行し、obj フォルダ以下の生成物を確認してください |
| 期待するサービスの登録がない | 名前空間の不足、またはタイポ | ファイルの先頭に `using AutoDiAttributes;` を追加し、属性名を確認してください |
| ライフタイムが正しく登録されない | 引数の指定順序の間違い | コンストラクタ引数は `(InjectServiceLifetime, Type)` の順序で指定してください |
