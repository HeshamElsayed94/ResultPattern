namespace ResultPattern.Results.Errors;

/// <summary>
/// Holds call-site information captured at the point where a <see cref="Error"/> was created.
/// </summary>
public readonly record struct ResultStackTrace(int LineNumber = 0, string FileName = "", string MemberName = "");