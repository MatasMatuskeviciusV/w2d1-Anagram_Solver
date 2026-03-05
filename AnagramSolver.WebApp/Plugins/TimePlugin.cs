namespace AnagramSolver.WebApp.Plugins;

using Microsoft.SemanticKernel;
using System.ComponentModel;

public class TimePlugin
{
    [KernelFunction("GetCurrentTime")]
    [Description("Gets the current date and time")]
    public string GetCurrentTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
