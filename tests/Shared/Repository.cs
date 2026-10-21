namespace Archie.Tests;

/// <summary>Files of the repository the tests run from, for tests that keep code, docs and schema in step.</summary>
internal static class Repository
{
    /// <summary>The folder with Archie.slnx.</summary>
    public static string Root { get; } = FindRoot();

    public static string PathOf(string relative) => System.IO.Path.Combine(Root, relative);

    public static string Read(string relative) => File.ReadAllText(PathOf(relative));

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(folder.FullName, "Archie.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No Archie.slnx above {AppContext.BaseDirectory}.");
    }
}
