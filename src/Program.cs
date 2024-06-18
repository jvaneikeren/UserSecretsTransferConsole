namespace UserSecretsTransferConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // TODO: Add command-line parameter checking and console output.

            if (args[0] == "-import")
            {
                UserSecretsTransferUtility.Import(
                    args[1],
                    args.Length < 3 ? null : args[2]);
            }
            else if (args[0] == "-export")
            {
                UserSecretsTransferUtility.Export(
                    args[1],
                    args[2],
                    args.Length < 4 ? null : args[3]);
            }
        }
    }
}
