using System;

[Serializable]
public class FastAPIError
{
    public string detail;
}

[Serializable]
public class HTTPValidationError
{
    public ValidationErrorItem[] detail;
}

[Serializable]
public class ValidationErrorItem
{
    public string msg;
    public string type;
}