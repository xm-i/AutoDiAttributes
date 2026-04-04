using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("AutoDiAttributes.Generator.Tests")]

namespace AutoDiAttributes.Generator;

internal static class NamespaceSanitizer {
	public static string SanitizeNamespace(string? name) {
		if (string.IsNullOrEmpty(name)) {
			return "GeneratedDI";
		}
		var sb = new StringBuilder(name!.Length);
		var first = name[0];
		if (char.IsLetter(first) || first == '_') {
			sb.Append(first);
		} else {
			sb.Append('_');
		}
		for (var i = 1; i < name.Length; i++) {
			var c = name[i];
			if (char.IsLetterOrDigit(c) || c == '_') {
				sb.Append(c);
			} else if (c == '.') {
				if (name[i - 1] == '.') {
					sb.Append('_');
				} else {
					sb.Append('.');
				}
			} else {
				sb.Append('_');
			}
		}
		var result = sb.ToString();
		if (result.Length > 0 && result[result.Length - 1] == '.') {
			result = result.Substring(0, result.Length - 1) + "_";
		}
		return result;
	}
}
