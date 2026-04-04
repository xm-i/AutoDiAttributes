using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
			.CreateSyntaxProvider(predicate: static (node, _) => node is AttributeSyntax attribute &&
					attribute.Name.ToString() == "InjectAttribute",
				transform: static (ctx, _) => GetRegistration(ctx))
			.Where(static r => r is { });

		var collected = registrations.Collect();

		var compilationAndCollected = context.CompilationProvider.Combine(collected);

		context.RegisterSourceOutput(compilationAndCollected, static (spc, data) => EmitSource(spc, data.Left, data.Right));
	}

	private static ServiceRegistration? GetRegistration(GeneratorSyntaxContext ctx) {
		var attributeSyntax = (AttributeSyntax)ctx.Node;
		if (attributeSyntax.Parent?.Parent is not ClassDeclarationSyntax cds) {
			return null;
		}

		if (ctx.SemanticModel.GetDeclaredSymbol(cds) is not INamedTypeSymbol symbol) {
			return null;
		}

		var implTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

		foreach (var attr in symbol.GetAttributes()) {
			if (attr.ApplicationSyntaxReference?.GetSyntax() == attributeSyntax) {
				// ServiceType
				var serviceTypeArg = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as INamedTypeSymbol : null;
				var serviceTypeName = serviceTypeArg != null ? serviceTypeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : implTypeName;

				// Lifetime
				var lifetime = attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int raw ? (InjectServiceLifetime)raw : InjectServiceLifetime.Transient;

				return new(serviceTypeName, implTypeName, lifetime);
			}
		}
		return null;
	}

	private static void EmitSource(SourceProductionContext context, Compilation compilation, ImmutableArray<ServiceRegistration?> registrations) {
		var linesBuilder = new StringBuilder();
		foreach (var reg in registrations) {
			if (reg is null) {
				continue;
			}

			switch (reg.Lifetime) {
				case InjectServiceLifetime.Transient:
					linesBuilder.Append("\t\tglobal::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddTransient<")
								.Append(reg.ServiceType)
								.Append(", ")
								.Append(reg.ImplType)
								.Append(">(services);\n");
					break;
				case InjectServiceLifetime.Scoped:
					linesBuilder.Append("\t\tglobal::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<")
								.Append(reg.ServiceType)
								.Append(", ")
								.Append(reg.ImplType)
								.Append(">(services);\n");
					break;
				case InjectServiceLifetime.Singleton:
					linesBuilder.Append("\t\tglobal::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<")
								.Append(reg.ServiceType)
								.Append(", ")
								.Append(reg.ImplType)
								.Append(">(services);\n");
					break;
			}
		}

		var assemblyName = compilation.AssemblyName;
		var ns = NamespaceSanitizer.SanitizeNamespace(assemblyName);

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
	private sealed record ServiceRegistration(string ServiceType, string ImplType, InjectServiceLifetime Lifetime);
}