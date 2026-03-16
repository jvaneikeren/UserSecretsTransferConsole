using Microsoft.Extensions.Configuration.UserSecrets;
using Orbital7.Extensions;
using Orbital7.Extensions.Encryption;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace UserSecretsTransferConsole;

public static class UserSecretsTransferUtility
{
    public static int Export(
        string solutionFilePath,
        string exportFilePath,
        string? password = null)
    {
        var projectFilePaths = ParseSolutionForProjectFilePaths(
            solutionFilePath);

        var userSecretsIds = GatherUserSecretsIds(
            projectFilePaths);

        CreateUserSecretsZipFile(
            userSecretsIds, 
            exportFilePath, 
            password);

        return userSecretsIds.Count;
    }

    public static int Import(
        string exportFilePath,
        string? password = null)
    {
        using (var zipFile = ZipFile.OpenRead(exportFilePath))
        {
            foreach (var entry in zipFile.Entries)
            {
                var secretsFilePath = PathHelper.GetSecretsPathFromSecretsId(entry.Name);
                var secretsId = Path.GetFileNameWithoutExtension(secretsFilePath);

                if (secretsId.HasText())
                {
                    using (var entryStream = entry.Open())
                    {
                        var contents = entryStream.ReadAll();
                        if (password.HasText())
                        {
                            contents = EncryptionHelper.Decrypt(
                                contents,
                                password,
                                EncryptionMethod.TripleDES);
                        }

                        FileSystemHelper.EnsureFolderExists(secretsId);
                        File.WriteAllBytes(
                            secretsFilePath, 
                            contents);
                    }
                }
            }

            return zipFile.Entries.Count;
        }
    }

    private static void CreateUserSecretsZipFile(
        List<string> userSecretsIds,
        string zipFilePath,
        string? password = null)
    {
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        using (var zipFile = ZipFile.Open(
            zipFilePath,
            ZipArchiveMode.Create))
        {
            foreach (var userSecretsId in userSecretsIds)
            {
                var secretsFilePath = PathHelper.GetSecretsPathFromSecretsId(userSecretsId);

                var contents = File.ReadAllBytes(secretsFilePath);
                if (password.HasText())
                {
                    contents = EncryptionHelper.Encrypt(
                        contents,
                        password,
                        EncryptionMethod.TripleDES);
                }

                var entry = zipFile.CreateEntry(userSecretsId);
                using (var entryStream = entry.Open())
                {
                    entryStream.Write(
                        contents, 
                        0, 
                        contents.Length);
                }
            }
        }
    }

    private static List<string> GatherUserSecretsIds(
        List<string> projectFilePaths)
    {
        var list = new List<string>();

        foreach (var projectFilePath in projectFilePaths)
        {
            var userSecretsId = TryGetUserSecretsId(projectFilePath);
            if (userSecretsId.HasText())
            {
                list.Add(userSecretsId);
            }
        }

        // Return the distinct list in case some secret files are shared.
        return list
            .Distinct()
            .ToList();
    }

    private static string? TryGetUserSecretsId(
        string projectFilePath)
    {
        // Avoid Microsoft.Build APIs that depend on Visual Studio private assemblies.
        // Load the project XML and look for a <UserSecretsId> element in any namespace.
        try
        {
            if (!File.Exists(projectFilePath))
            {
                return null;
            }

            var doc = XDocument.Load(projectFilePath);

            // Find the first element whose local name equals "UserSecretsId".
            var userSecretsElement = doc
                .Descendants()
                .FirstOrDefault(x => string.Equals(
                    x.Name.LocalName, 
                    "UserSecretsId", 
                    StringComparison.OrdinalIgnoreCase));

            var value = userSecretsElement?.Value?.Trim();

            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch
        {
            // Be defensive: if the project file is malformed or unreadable, return null.
            return null;
        }
    }

    private static List<string> ParseSolutionForProjectFilePaths(
        string solutionFilePath)
    {
        // NOTE: We used to use Microsoft.Build for this, but as of version 18.x,
        // the assembly used for this is private and the code below will error out.
        //
        //var solutionFile = SolutionFile.Parse(solutionFilePath);
        //var projectFilePaths = solutionFile.ProjectsInOrder
        //    .Where(x => x.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
        //    .Select(x => x.AbsolutePath)
        //    .ToList();
        //
        // ...and thus, we now need to parse the solution file ourselves.

        
        var solutionFileExtension = Path.GetExtension(solutionFilePath);
        var solutionFolderPath = Path.GetDirectoryName(solutionFilePath) ?? string.Empty;

        // Common project extensions considered MSBuild projects.
        var projectFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj", ".vbproj", ".fsproj", ".vcxproj", ".shproj"
        };

        // Handle by solution file extension which indicates the format of the solution file.
        if (solutionFileExtension == ".sln")
        {
            return ParseSlnFileForProjectFilePaths(
                solutionFilePath,
                solutionFolderPath,
                projectFileExtensions);
        }
        else if (solutionFileExtension == ".slnx")
        {
            return ParseSlnxFileForProjectFilePaths(
                solutionFilePath, 
                solutionFolderPath, 
                projectFileExtensions);
        }
        else
        {
            throw new Exception($"Unrecognized solution file extension: {solutionFileExtension}");
        }
    }

    private static List<string> ParseSlnFileForProjectFilePaths(
        string slnFilePath,
        string solutionFolderPath,
        HashSet<string> acceptedExtensions)
    {
        var list = new List<string>();

        // Project("{...}") = "Name", "path\to\proj.csproj", "{...}"
        var projectLineRegex = new Regex(
            "^Project\\(\"(?<typeGuid>[^\"]+)\"\\)\\s*=\\s*\"(?<name>[^\"]+)\"\\s*,\\s*\"(?<path>[^\"]+)\"\\s*,\\s*\"(?<guid>[^\"]+)\"",
            RegexOptions.Compiled);

        foreach (var line in File.ReadLines(slnFilePath))
        {
            var match = projectLineRegex.Match(line);
            if (!match.Success) continue;

            var relativePath = match.Groups["path"].Value
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var resolvedPath = Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.GetFullPath(Path.Combine(solutionFolderPath, relativePath));

            if (File.Exists(resolvedPath) && acceptedExtensions.Contains(Path.GetExtension(resolvedPath)))
            {
                list.Add(resolvedPath);
            }
        }

        return list;
    }

    private static List<string> ParseSlnxFileForProjectFilePaths(
        string slnxFilePath,
        string solutionFolderPath,
        HashSet<string> acceptedExtensions)
    {
        var list = new List<string>();

        var doc2 = XDocument.Load(slnxFilePath);
        var projectPaths = doc2
          .Descendants()
          .Where(e => string.Equals(e.Name.LocalName, "Project", StringComparison.OrdinalIgnoreCase))
          .Select(e => (string?)e.Attribute("Path"))
          .Where(p => !string.IsNullOrEmpty(p))
          .ToList();

        foreach (var projectPath in projectPaths)
        {
            var normalizedProjectPath = projectPath?.Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            if (normalizedProjectPath.HasText())
            {
                var resolvedPath = Path.IsPathRooted(normalizedProjectPath) ?
                    normalizedProjectPath :
                    Path.GetFullPath(Path.Combine(solutionFolderPath, normalizedProjectPath));

                if (File.Exists(resolvedPath) && 
                    acceptedExtensions.Contains(Path.GetExtension(resolvedPath)))
                {
                    list.Add(resolvedPath);
                }
            }
        }

        return list;
    }
}
