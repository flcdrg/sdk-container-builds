namespace Microsoft.NET.Build.Containers;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public class CreateNewImageToolTask : ToolTask
{
    [Required]
    public string ToolDirectory { get; set; }
    protected override string ToolName => "cne.exe";

    public CreateNewImageToolTask()
    {
        ToolDirectory = "";
    }

    protected override string GenerateFullPathToTool()
    {
        
        throw new NotImplementedException();
    }

    protected override string GenerateCommandLineCommands()
    {
        return ""; // Pass all parameters required for CreateNewImage. The task will call our API. This is that task...
    }
}