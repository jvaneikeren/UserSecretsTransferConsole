namespace UserSecretsTransferConsole;

internal class Program
{
    static void Main(string[] args)
    {
        if (args.Length >= 2)
        {
            var command = args[0].ToLower();

            // Handle import.
            if (command == "-import")
            {
                int count = UserSecretsTransferUtility.Import(
                    args[1],
                    args.Length < 3 ? null : args[2]);

                Console.WriteLine($"Successfully imported {count} UserSecrets file(s)");
            }
            // Handle export.
            else if (command == "-export")
            {
                int count = UserSecretsTransferUtility.Export(
                    args[1],
                    args[2],
                    args.Length < 4 ? null : args[3]);

                Console.WriteLine($"Successfully exported {count} UserSecrets file(s)");
            }
            else
            {
                ShowUsage();
            }
        }
        else
        {
            ShowUsage();
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  To export UserSecrets files:");
        Console.WriteLine("    UserSecretsTransferConsole -export <solutionFilePath> <exportFilePath> [password]");
        Console.WriteLine("  To import UserSecrets files:");
        Console.WriteLine("    UserSecretsTransferConsole -import <exportFilePath> [password]");
    }
}
