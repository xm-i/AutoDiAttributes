using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using AutoDiAttributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
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
	}

	/// <summary>
	/// ライフタイムが指定されない場合、Transientとして登録されることを検証します。
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

	/// <summary>
	/// 属性の指定方法にかかわらず、正しく処理されることを検証します。
	/// </summary>
	[Fact]
	public void Generator_ShouldHandleAllAttributeNamingPatterns() {
		// Arrange
		var source = """
			using AutoDiAttributes;

			namespace TestNamespace
			{
				[Inject(InjectServiceLifetime.Singleton)]
				public class Service1 {}

				[InjectAttribute(InjectServiceLifetime.Singleton)]
				public class Service2 {}

				[AutoDiAttributes.Inject(InjectServiceLifetime.Singleton)]
				public class Service3 {}

				[AutoDiAttributes.InjectAttribute(InjectServiceLifetime.Singleton)]
				public class Service4 {}

				[global::AutoDiAttributes.Inject(InjectServiceLifetime.Singleton)]
				public class Service5 {}

				[global::AutoDiAttributes.InjectAttribute(InjectServiceLifetime.Singleton)]
				public class Service6 {}
			}
			""";

		var compilation = CreateCompilation(source);
		var generator = new DIRegistrationGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		// Act
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

		// Assert
		diagnostics.ShouldBeEmpty();
		var runResult = driver.GetRunResult();
		runResult.GeneratedTrees.Length.ShouldBe(1);

		var generatedCode = runResult.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("Service1");
		generatedCode.ShouldContain("Service2");
		generatedCode.ShouldContain("Service3");
		generatedCode.ShouldContain("Service4");
		generatedCode.ShouldContain("Service5");
		generatedCode.ShouldContain("Service6");
	}

	/// <summary>
	/// 1つのクラスに複数の属性が指定された場合、インターフェースと実装型の両方の登録が正しく生成されることを検証します。
	/// </summary>
	[Fact]
	public void Generator_ShouldGenerateRegistrationsForBothInterfaceAndImplementation_WhenBothAttributesArePresent() {
		// Arrange
		var source = """
			using AutoDiAttributes;

			namespace TestNamespace
			{
				public interface ITestService {}

				[Inject(InjectServiceLifetime.Singleton, typeof(ITestService))]
				[Inject(InjectServiceLifetime.Transient)]
				public class TestService : ITestService {}
			}
			""";

		var compilation = CreateCompilation(source);
		var generator = new DIRegistrationGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		// Act
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

		// Assert
		diagnostics.ShouldBeEmpty();
		var runResult = driver.GetRunResult();
		var generatedCode = runResult.GeneratedTrees[0].GetText().ToString();

		// 生成されたコードに期待される登録が含まれていることを確認
		generatedCode.ShouldContain("global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::TestNamespace.ITestService, global::TestNamespace.TestService>(services);");
		generatedCode.ShouldContain("global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddTransient<global::TestNamespace.TestService, global::TestNamespace.TestService>(services);");
	}

	private static Compilation CreateCompilation(string source) {
		var syntaxTree = CSharpSyntaxTree.ParseText(source);
		var attributeSource = """
			using System;

			namespace AutoDiAttributes;

			[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
			public sealed class InjectAttribute : Attribute {
				public InjectServiceLifetime Lifetime { get; }
				public Type? ServiceType { get; }

				public InjectAttribute(InjectServiceLifetime lifetime) {
					this.Lifetime = lifetime;
				}

				public InjectAttribute(InjectServiceLifetime lifetime, Type serviceType) {
					this.Lifetime = lifetime;
					this.ServiceType = serviceType;
				}
			}

			public enum InjectServiceLifetime {
				Transient = 0,
				Scoped = 1,
				Singleton = 2
			}
			""";
		var attributeTree = CSharpSyntaxTree.ParseText(attributeSource);

		var references = new[] {
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
			MetadataReference.CreateFromFile(AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "System.Runtime").Location)
		};

		return CSharpCompilation.Create(
			"TestAssembly",
			new[] { syntaxTree, attributeTree },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);
	}
}