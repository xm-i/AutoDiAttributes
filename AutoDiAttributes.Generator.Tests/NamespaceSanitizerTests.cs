namespace AutoDiAttributes.Generator.Tests;

public class NamespaceSanitizerTests {
	[Theory]
	[InlineData(null, "GeneratedDI")]
	[InlineData("", "GeneratedDI")]
	[InlineData("MyNamespace", "MyNamespace")]
	[InlineData("My.Namespace", "My.Namespace")]
	[InlineData("123Namespace", "_23Namespace")]
	[InlineData("My-Namespace", "My_Namespace")]
	[InlineData("My..Namespace", "My._Namespace")]
	[InlineData(".Namespace", "_Namespace")]
	[InlineData("Namespace.", "Namespace_")]
	[InlineData("_Namespace", "_Namespace")]
	[InlineData("My Namespace", "My_Namespace")]
	[InlineData("My#Namespace", "My_Namespace")]
	public void SanitizeNamespace_ShouldReturnExpectedResult(string? input, string expected) {
		// Act
		var result = NamespaceSanitizer.SanitizeNamespace(input);

		// Assert
		Assert.Equal(expected, result);
	}
}