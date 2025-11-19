namespace AutoDiAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class InjectAttribute : Attribute {
	public InjectServiceLifetime Lifetime {
		get;
	}
	public Type? ServiceType {
		get;
	}

	public InjectAttribute(InjectServiceLifetime lifetime) {
		this.Lifetime = lifetime;
	}

	public InjectAttribute(InjectServiceLifetime lifetime, Type serviceType) {
		this.Lifetime = lifetime;
		this.ServiceType = serviceType;
	}
}
