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
		for (var i = 0; i < name.Length; i++) {
			var c = name[i];
			if (i == 0) {
				if (char.IsLetter(c) || c == '_') {
					sb.Append(c);
				} else {
					sb.Append('_');
				}
			} else {
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
		}
		var result = sb.ToString();
		if (result.Length > 0 && result[result.Length - 1] == '.') {
			result = result.Substring(0, result.Length - 1) + "_";
		}
		return result;
	}
}
