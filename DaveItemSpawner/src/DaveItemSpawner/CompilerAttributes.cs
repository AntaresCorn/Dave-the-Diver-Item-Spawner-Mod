namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Event | AttributeTargets.Field | AttributeTargets.GenericParameter | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue)]
internal sealed class NullableAttribute : Attribute
{
    public NullableAttribute(byte value)
    {
        NullableFlags = new[] { value };
    }

    public NullableAttribute(byte[] value)
    {
        NullableFlags = value;
    }

    public byte[] NullableFlags { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Struct)]
internal sealed class NullableContextAttribute : Attribute
{
    public NullableContextAttribute(byte value)
    {
        Flag = value;
    }

    public byte Flag { get; }
}
