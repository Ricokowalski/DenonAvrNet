namespace DenonAvrNet.Exceptions;

/// <summary>Base class for errors reported by DenonAvrNet.</summary>
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

/// <summary>Indicates that no supported receiver endpoint could be reached.</summary>
public sealed class DenonConnectionException : DenonAvrException
{
    public DenonConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Indicates an invalid or incomplete receiver protocol response.</summary>
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
