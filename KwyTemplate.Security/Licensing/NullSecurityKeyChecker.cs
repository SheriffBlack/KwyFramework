namespace KwyTemplate.Security.Licensing;

internal sealed class NullSecurityKeyChecker : ISecurityKeyChecker
{
    public bool IsPresent() => true;
}

