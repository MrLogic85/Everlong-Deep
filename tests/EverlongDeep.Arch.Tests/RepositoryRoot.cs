namespace EverlongDeep.Arch.Tests;

internal static class RepositoryRoot
{
    /// <summary>The nearest parent directory of the test binaries that holds EverlongDeep.slnx.</summary>
    internal static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EverlongDeep.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find EverlongDeep.slnx.");
    }
}
