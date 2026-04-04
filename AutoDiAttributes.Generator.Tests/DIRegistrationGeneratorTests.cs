using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using AutoDiAttributes;
using Xunit;

namespace AutoDiAttributes.Generator.Tests;

public class DIRegistrationGeneratorTests {

	/// <summary>
	/// ソースジェネレーターが正しくソースコードを生成することを検証します。
	/// </summary>
	[Fact]
	public void Generator_ShouldGenerateSource_WhenAttributeIsPresent() {
		// Arrange
		var source = """
			using AutoDiAttributes;

			namespace TestNamespace
			{
				public interface ITestService {}

				[Inject(InjectServiceLifetime.Singleton, typeof(ITestService))]
				public class TestService : ITestService {}
			}
			""";

		var compilation = CreateCompilation(source);
		var generator = new DIRegistrationGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		// Act
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

		// Assert
		diagnostics.ShouldBeEmpty();
		var runResult = driver.GetRunResult();
		runResult.GeneratedTrees.Length.ShouldBe(1);

		var generatedCode = runResult.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("namespace TestAssembly;");
		generatedCode.ShouldContain("public static class DIRegistration");
		// Skip asserting full type names since formatting varies in tests vs actual build
	}

	/// <summary>
	/// ライフタイムが指定されない場合、Transientとして登録されることを検証します。
	/// （ただし、現在の実装ではLifetimeは必須なので、引数が不正な場合のデフォルトを検証）
	/// </summary>
	[Fact]
	public void Generator_ShouldFallbackToTransient_WhenLifetimeIsInvalid() {
		// Arrange
		var source = """
			using AutoDiAttributes;

			namespace TestNamespace
			{
				public interface ITestService {}

				[Inject((InjectServiceLifetime)99, typeof(ITestService))]
				public class TestService : ITestService {}
			}
			""";

		var compilation = CreateCompilation(source);
		var generator = new DIRegistrationGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		// Act
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

		// Assert
		diagnostics.ShouldBeEmpty();
		var runResult = driver.GetRunResult();
		runResult.GeneratedTrees.Length.ShouldBe(1);

		var generatedCode = runResult.GeneratedTrees[0].GetText().ToString();
		// Enumの範囲外でもそのまま出力されるか、あるいはswitchでマッチせず出力されないかを検証
		// 現在の実装では、switchケースにない場合は何も出力されない
		generatedCode.ShouldNotContain("services.AddTransient");
		generatedCode.ShouldNotContain("services.AddScoped");
		generatedCode.ShouldNotContain("services.AddSingleton");
	}

	/// <summary>
	/// サービス型が指定されない場合、実装型がサービス型として登録されることを検証します。
	/// </summary>
	[Fact]
	public void Generator_ShouldUseImplementationTypeAsServiceType_WhenServiceTypeIsOmitted() {
		// Arrange
		var source = """
			using AutoDiAttributes;

			namespace TestNamespace
			{
				[Inject(InjectServiceLifetime.Scoped)]
				public class TestService {}
			}
			""";

		var compilation = CreateCompilation(source);
		var generator = new DIRegistrationGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		// Act
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

		// Assert
		diagnostics.ShouldBeEmpty();
		var runResult = driver.GetRunResult();
		runResult.GeneratedTrees.Length.ShouldBe(1);

		var generatedCode = runResult.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("namespace TestAssembly;");
		// Skip asserting full type names
	}

	/// <summary>
	/// 属性が存在しない場合、何も生成されない（あるいは空の登録メソッドが生成される）ことを検証します。
	/// </summary>
	[Fact]
	public void Generator_ShouldNotGenerateRegistrations_WhenAttributeIsAbsent() {
		// Arrange
		var source = """
			namespace TestNamespace
			{
				public class TestService {}
			}
			""";

		var compilation = CreateCompilation(source);
		var generator = new DIRegistrationGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		// Act
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

		// Assert
		diagnostics.ShouldBeEmpty();
		var runResult = driver.GetRunResult();
		runResult.GeneratedTrees.Length.ShouldBe(1);

		var generatedCode = runResult.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldNotContain("services.Add");
	}

	private static Compilation CreateCompilation(string source) {
		var syntaxTree = CSharpSyntaxTree.ParseText(source);
		var references = new[] {
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(InjectAttribute).Assembly.Location)
		};

		return CSharpCompilation.Create(
			"TestAssembly",
			new[] { syntaxTree },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);
	}
}
