# UserSecretsTransferConsole

Command-line console application to import/export user secrets for a given Visual Studio solution. The application will export user secrets to a zip file (with an optional password to encrypt the individual secret files contents); you can then use the application to import the user secrets zip file on a different workstation.

To export user secrets for a solution:

```
USAGE: UserSecretsTransferConsole.exe -export [OutputZipFilePath] [OptionalPassword]
```

To import user secrets for a solution:

```
USAGE: UserSecretsTransferConsole.exe -import [ExportedZipFilePath] [PasswordUsedForExportIfAny]
```
