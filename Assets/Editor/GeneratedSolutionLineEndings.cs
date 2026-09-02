using UnityEditor;

/// <summary>
/// Writes the generated solution file with LF line endings.
///
/// com.unity.ide.visualstudio assembles it with a hardcoded CRLF terminator
/// (SdkStyleProjectGeneration.SolutionText), on every platform, so the file lands on disk
/// against .editorconfig and .gitattributes -- which both ask for LF -- and the working tree
/// shows BROcoli.slnx modified after every project sync. The package has no setting for this;
/// OnGeneratedSlnSolution is the hook it offers, and what the hook returns is what gets written.
///
/// The byte order mark is out of reach from here: the package writes through its own file
/// provider with Encoding.UTF8, so the file keeps the BOM it is committed with.
/// </summary>
internal sealed class GeneratedSolutionLineEndings : AssetPostprocessor
{
    private static string OnGeneratedSlnSolution(string path, string content)
    {
        return content.Replace("\r\n", "\n");
    }
}
