using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Collections.Immutable;

namespace AutoDiAttributes.Generator;

[Generator]
public sealed class DIRegistrationGenerator : IIncrementalGenerator {
	private enum InjectServiceLifetime {
		Transient = 0,
		Scoped = 1,
		Singleton = 2
	}

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		var registrations = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
				transform: static (ctx, _) => GetRegistration(ctx))
			.Where(static r => r is not null)!;

		var collected = registrations.Collect();

		var compilationAndCollected = context.CompilationProvider.Combine(collected);

		context.RegisterSourceOutput(compilationAndCollected, static (spc, data) => EmitSource(spc, data.Left, data.Right!));
	}

	private static ServiceRegistration? GetRegistration(GeneratorSyntaxContext ctx) {
		if (ctx.Node is not ClassDeclarationSyntax cds) {
			return null;
		}
		if (ctx.SemanticModel.GetDeclaredSymbol(cds) is not INamedTypeSymbol symbol) {
			return null;
		}

		foreach (var attr in symbol.GetAttributes()) {
			var name = attr.AttributeClass?.Name;
			if (name != "InjectAttribute") {
				continue;
			}

			// ServiceType
			var serviceTypeArg = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as INamedTypeSymbol : null;
			var serviceTypeName = serviceTypeArg != null
				? serviceTypeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
				: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			var implTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

			// Lifetime
			var lifetime = attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int raw
				? (InjectServiceLifetime)raw
				: InjectServiceLifetime.Transient;

			return new ServiceRegistration(serviceTypeName, implTypeName, lifetime);
		}
		return null;
	}

	private static void EmitSource(SourceProductionContext context, Compilation compilation, ImmutableArray<ServiceRegistration?> registrations) {
		var linesBuilder = new StringBuilder();
		foreach (var reg in registrations) {
			if (reg is null) {
				continue;
			}

			var line = reg.Lifetime switch {
				InjectServiceLifetime.Transient => $"\t\tglobal::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddTransient<{reg.ServiceType}, {reg.ImplType}>(services);\n",
				InjectServiceLifetime.Scoped => $"\t\tglobal::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<{reg.ServiceType}, {reg.ImplType}>(services);\n",
				InjectServiceLifetime.Singleton => $"\t\tglobal::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<{reg.ServiceType}, {reg.ImplType}>(services);\n",
				_ => null
			};
			if (line != null) {
				linesBuilder.Append(line);
			}
		}

		var assemblyName = compilation.AssemblyName;
		var ns = SanitizeNamespace(assemblyName);

		var lines = linesBuilder.ToString();
		
		var source = $$"""
namespace {{ns}};
public static class DIRegistration
{
    public static void AddGeneratedServices(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
{{lines}}    }
}
""";
		context.AddSource("DIRegistration.g.cs", source);
	}

	private static string SanitizeNamespace(string? name) {
		if (string.IsNullOrEmpty(name)) {
			return "GeneratedDI";
		}
		var sb = new StringBuilder(name!.Length);
		for (var i = 0; i < name.Length; i++) {
			var c = name[i];
			if (i == 0) {
				if (char.IsLetter(c) || c == '_') {
					sb.Append(c);
				} else {
					sb.Append('_');
				}
			} else {
				if (char.IsLetterOrDigit(c) || c == '_' || (c == '.' && name[i-1] != '.')) {
					sb.Append(c);
				} else {
					sb.Append('_');
				}
			}
		}
		return sb.ToString();
	}

	private sealed class ServiceRegistration(string ServiceType, string ImplType, InjectServiceLifetime Lifetime) {
		public string ServiceType { get; } = ServiceType;
		public string ImplType { get; } = ImplType;
		public InjectServiceLifetime Lifetime { get; } = Lifetime;
	}
}
