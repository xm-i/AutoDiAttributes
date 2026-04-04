using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace AutoDiAttributes.Generator.Tests;

public class BenchmarkTest {
	private readonly ITestOutputHelper _output;

	public BenchmarkTest(ITestOutputHelper output) {
		this._output = output;
	}

	[Fact]
	public void BenchmarkGetRegistration() {
		// 1000個のクラスを含むソースを生成
		var sourceBuilder = new System.Text.StringBuilder();
		sourceBuilder.AppendLine("using AutoDiAttributes;");
		sourceBuilder.AppendLine("namespace TestApp {");
		for (var i = 0; i < 10000; i++) {
			sourceBuilder.AppendLine($"  [Inject({i % 3})]");
			sourceBuilder.AppendLine($"  public class TestService{i} {{ }}");
		}
		sourceBuilder.AppendLine("}");

		var source = sourceBuilder.ToString();
		var syntaxTree = CSharpSyntaxTree.ParseText(source);

		var references = new[]
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
			MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
			MetadataReference.CreateFromFile(typeof(AutoDiAttributes.Generator.DIRegistrationGenerator).Assembly.Location)
		};

		var compilation = CSharpCompilation.Create("TestComp", new[] { syntaxTree }, references);
		var semanticModel = compilation.GetSemanticModel(syntaxTree);

		var classDeclarations = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
		var attributeSyntaxes = syntaxTree.GetRoot().DescendantNodes().OfType<AttributeSyntax>().ToList();

		var generatorType = typeof(AutoDiAttributes.Generator.DIRegistrationGenerator);
		var getRegistrationMethod = generatorType.GetMethod("GetRegistration", BindingFlags.NonPublic | BindingFlags.Static);

		var ctxType = typeof(GeneratorSyntaxContext);
		var ctxConstructors = ctxType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
		var ctxConstructor = ctxConstructors.FirstOrDefault();

		// null passing for the 3rd parameter (might be CancellationToen or something)
		object CreateContext(SyntaxNode node, SemanticModel sm) {
			var lazySm = new Lazy<SemanticModel>(() => sm);
			var syntaxHelperType = typeof(GeneratorSyntaxContext).Assembly.GetType("Microsoft.CodeAnalysis.ISyntaxHelper");
			var isyntaxHelper = default(object); // might be null or maybe we need actual syntax helper
			return ctxConstructor.Invoke(new object[] { node, lazySm, isyntaxHelper });
		}

		var contexts = attributeSyntaxes.Select(attr => CreateContext(attr, semanticModel)).ToArray();

		// ウォームアップ
		for (var i = 0; i < 100; i++) {
			getRegistrationMethod.Invoke(null, new object[] { contexts[i] });
		}

		// 計測
		var sw = Stopwatch.StartNew();
		foreach (var ctx in contexts) {
			getRegistrationMethod.Invoke(null, new object[] { ctx });
		}
		sw.Stop();

		this._output.WriteLine($"[Benchmark Result] Elapsed: {sw.ElapsedMilliseconds}ms for {contexts.Length} nodes");
	}
}