namespace PadesSign.Domain.ValueObjects;

public sealed record SignatureFieldDefinition(
    int    PageNumber,
    float  X,
    float  Y,
    float  Width,
    float  Height,
    string Reason,
    string Location,
    string ContactInfo = "");