namespace DenonAvrNet.Exceptions;

public class DenonAvrException : Exception
{
    public DenonAvrException(string message) : base(message)
    {
    }

    public DenonAvrException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class DenonConnectionException : DenonAvrException
{
    public DenonConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class DenonProtocolException : DenonAvrException
{
    public DenonProtocolException(string message) : base(message)
    {
    }

    public DenonProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
