/*using System.IO;

namespace ISCSI_Util.Utils;

public static class FileSystemUtils
{
    public static string CrearCarpetaMontaje(string iqn)
    {
        string basePath = "/mnt/iscsi";
        string folderName = SanitizarNombre(iqn);
        string fullPath = Path.Combine(basePath, folderName);

        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);

        return fullPath;
    }

    public static string SanitizarNombre(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');
        return input;
    }
}



public static class Credenciales
{
    public static string AdminPassword { get; set; }
}
*/


using System.IO;

namespace ISCSI_Util.Utils;

public static class FileSystemUtils
{
    public static string CrearCarpetaMontaje(string iqn)
    {
        // 🔧 Cambiado: ahora la base es /home/iscsi
        string basePath = "/home/iscsi";
        string folderName = SanitizarNombre(iqn);
        string fullPath = Path.Combine(basePath, folderName);

        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);

        return fullPath;
    }

    public static string SanitizarNombre(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "iscsi";

        // Reemplazar caracteres inválidos por '_'
        foreach (var c in Path.GetInvalidFileNameChars())
            input = input.Replace(c, '_');

        // Reemplazar ':' explícitamente (Windows no lo permite en nombres de carpeta)
        input = input.Replace(':', '_');

        return input;
    }
}

public static class Credenciales
{
    public static string AdminPassword { get; set; }
}
