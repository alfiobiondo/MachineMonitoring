using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MachineMonitoring.Application.Exceptions;

public class MachineUnavailableException : Exception
{
    public MachineUnavailableException(string message)
        : base(message) { }

    public MachineUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
