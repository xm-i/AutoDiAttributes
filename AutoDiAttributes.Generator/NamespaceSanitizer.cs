using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("AutoDiAttributes.Generator.Tests")]

namespace AutoDiAttributes.Generator;

internal static class NamespaceSanitizer {
	public static string SanitizeNamespace(string? name) {
		if (name is null || name.Length == 0) {
			return "GeneratedDI";
		}
		var sb = new StringBuilder(name.Length);
		var first = name[0];
		sb.Append(char.IsLetter(first) || first == '_' ? first : '_');

		for (var i = 1; i < name.Length; i++) {
			var c = name[i];
			if (char.IsLetterOrDigit(c) || c == '_') {
				sb.Append(c);
			} else if (c == '.' && name[i - 1] != '.') {
				sb.Append('.');
			} else {
				sb.Append('_');
			}
		}

		var result = sb.ToString();
		if (result.EndsWith(".")) {
			result = result.Substring(0, result.Length - 1) + "_";
		}
		return result;
	}
}