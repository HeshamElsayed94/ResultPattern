namespace ResultPattern.Results.Errors;



public readonly record  struct  ResultStackTrace(int LineNumber = 0 , string FileName ="" , string MemberName ="");