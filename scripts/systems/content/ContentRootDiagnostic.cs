internal enum ReferenceRole
{
    Owned = 0,
    Borrowed,
    Transferred,
}

internal sealed record ContentRootDiagnostic(
    string CanonicalPath,
    string ResourceType,
    ReferenceRole Role
);
