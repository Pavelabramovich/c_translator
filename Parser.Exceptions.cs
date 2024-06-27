using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Trans;


public class LexicalException : Exception
{
    public LexicalException()
        : base()
    { }

    public LexicalException(string message)
        : base(message)
    { }

    public LexicalException(string message, Exception innerException)
        : base(message, innerException)
    { }
}


public class SyntaxException : Exception
{
    public SyntaxException()
        : base()
    { }

    public SyntaxException(string message)
        : base(message)
    { }

    public SyntaxException(string message, Exception innerException)
        : base(message, innerException)
    { }
}


public class SemanticException : Exception
{
    public SemanticException()
        : base()
    { }

    public SemanticException(string message)
        : base(message)
    { }

    public SemanticException(string message, Exception innerException)
        : base(message, innerException)
    { }
}


public class InterpretationException : Exception
{
    public InterpretationException()
        : base()
    { }

    public InterpretationException(string message)
        : base(message)
    { }

    public InterpretationException(string message, Exception innerException)
        : base(message, innerException)
    { }
}



public class UnexpectedException : Exception;