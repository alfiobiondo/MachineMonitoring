using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MachineMonitoring.Application;

public class InvalidMachineStateException : Exception
{
    public InvalidMachineStateException(string message)
        : base(message) { }

    public InvalidMachineStateException(string message, Exception innerException)
        : base(message, innerException) { }
}
