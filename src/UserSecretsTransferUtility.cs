using Microsoft.Build.Construction;
using Microsoft.Extensions.Configuration.UserSecrets;
using Orbital7.Extensions.Encryption;
using System.IO.Compression;

namespace UserSecretsTransferConsole;

public static class UserSecretsTransferUtility
{
    public static int Export(
        string solutionFilePath,
        string exportFilePath,
        string password = null)
    {
        var solutionFile = SolutionFile.Parse(solutionFilePath);
        var projectFilePaths = solutionFile.ProjectsInOrder
            .Where(x => x.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            .Select(x => x.AbsolutePath)
            .ToList();

        var userSecretsIds = GatherUserSecretsIds(projectFilePaths);

        CreateUserSecretsZipFile(userSecretsIds, exportFilePath, password);

        return userSecretsIds.Count;
    }

    public static int Import(
        string exportFilePath,
        string password = null)
    {
        using (var zipFile = ZipFile.OpenRead(exportFilePath))
        {
            foreach (var entry in zipFile.Entries)
            {
                var secretsFilePath = PathHelper.GetSecretsPathFromSecretsId(entry.Name);

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

                    FileSystemHelper.EnsureFolderExists(Path.GetDirectoryName(secretsFilePath));
                    File.WriteAllBytes(secretsFilePath, contents);
                }
            }

            return zipFile.Entries.Count;
        }
    }

    private static void CreateUserSecretsZipFile(
        List<string> userSecretsIds,
        string zipFilePath,
        string password = null)
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
                    entryStream.Write(contents, 0, contents.Length);
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
            var userSecretsId = GetUserSecretsId(projectFilePath);
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

    private static string GetUserSecretsId(
        string projectFilePath)
    {
        var projectFile = ProjectRootElement.Open(projectFilePath);

        foreach (var propertyGroup in projectFile.PropertyGroups)
        {
            foreach (var property in propertyGroup.Properties)
            {
                if (property.Name == "UserSecretsId")
                {
                    return property.Value;
                }
            }
        }

        return null;
    }
}
