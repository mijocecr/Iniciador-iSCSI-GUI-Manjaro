/*using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ISCSI_Util.Models;
using ISCSI_Util.Utils;

namespace ISCSI_Util.Helpers;

public static class IscsiHelper
{
    ///////////////////////////////////////////////////////////////
    #region Descubrir_Destinos

    public static List<IscsiDestino> Descubrir(string ip)
    {
        var destinos = new List<IscsiDestino>();

        try
        {
            // 1. Discovery de IQN en el portal
            string output = Ejecutar("pkexec", $"iscsiadm -m discovery -t sendtargets -p {ip}");
            string sesionesOut = Ejecutar("pkexec", "iscsiadm -m session");
            var sesiones = sesionesOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                string iqn = tokens[^1];
                bool conectado = sesiones.Any(s => s.Contains(iqn));

                // Evitar duplicados
                if (destinos.Any(d => d.Iqn == iqn && d.Ip == ip))
                    continue;

                var destino = new IscsiDestino
                {
                    Ip = ip,
                    Iqn = iqn,
                    Conectado = conectado,
                    Seleccionado = false // nunca marcado al descubrir
                };

                // Intentar localizar symlink en /dev/disk/by-path/
                var byPath = Ejecutar("ls", "-1 /dev/disk/by-path/");
                foreach (var dev in byPath.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (dev.Contains(iqn) && dev.Contains("lun"))
                    {
                        destino.DevicePath = "/dev/disk/by-path/" + dev.Trim();
                        break;
                    }
                }

                destinos.Add(destino);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descubrir destinos: {ex.Message}");
        }

        return destinos;
    }

    // Helper genérico
    private static string Ejecutar(string fileName, string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(error))
            Console.WriteLine($"Error ejecutando {fileName}: {error}");

        return output;
    }

    #endregion
    ///////////////////////////////////////////////////////////////

  
   public static void Conectar(IscsiDestino destino)
{
    try
    {
        // 0. Verificar si ya hay sesión activa
        var sesionesOut = Ejecutar("pkexec", "iscsiadm -m session");
        bool yaConectado = sesionesOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                      .Any(s => s.Contains(destino.Iqn));

        if (!yaConectado)
        {
            // 1. Login al destino solo si no está conectado
            Ejecutar("pkexec", $"iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --login");
        }

        // 2. Crear carpeta de montaje
        destino.MountPoint = $"/mnt/iscsi/{FileSystemUtils.SanitizarNombre(destino.Iqn)}";
        Ejecutar("pkexec", $"mkdir -p {destino.MountPoint}");

        // 3. Buscar symlink real en /dev/disk/by-path
        var output = Ejecutar("ls", "-1 /dev/disk/by-path/");
        foreach (var line in output.Split('\n'))
        {
            if (line.Contains(destino.Iqn) && line.Contains("lun"))
            {
                destino.DevicePath = "/dev/disk/by-path/" + line.Trim();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(destino.DevicePath))
            throw new InvalidOperationException($"No se encontró symlink para el destino {destino.Iqn}.");

        // 4. Si apunta al disco, buscar partición
        var lsblkOut = Ejecutar("lsblk", "-rno NAME " + destino.DevicePath);
        var lines = lsblkOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
        {
            destino.DevicePath = "/dev/" + lines[1].Trim(); // primera partición
        }

        // 5. Detectar filesystem automáticamente
        var blkidOut = Ejecutar("blkid", destino.DevicePath);
        string fsType = "ext4"; // fallback
        if (blkidOut.Contains("TYPE=\"ext2\"")) fsType = "ext2";
        else if (blkidOut.Contains("TYPE=\"ext3\"")) fsType = "ext3";
        else if (blkidOut.Contains("TYPE=\"ext4\"")) fsType = "ext4";
        else if (blkidOut.Contains("TYPE=\"xfs\"")) fsType = "xfs";
        else if (blkidOut.Contains("TYPE=\"btrfs\"")) fsType = "btrfs";
        else if (blkidOut.Contains("TYPE=\"f2fs\"")) fsType = "f2fs";
        else if (blkidOut.Contains("TYPE=\"ntfs\"")) fsType = "ntfs";
        else if (blkidOut.Contains("TYPE=\"vfat\"")) fsType = "vfat";
        else if (blkidOut.Contains("TYPE=\"exfat\"")) fsType = "exfat";
        else if (blkidOut.Contains("TYPE=\"iso9660\"")) fsType = "iso9660";

        // 6. Montar el dispositivo
        Ejecutar("pkexec", $"mount -t {fsType} {destino.DevicePath} {destino.MountPoint}");

        // 7. Ajustar grupo y permisos dinámicamente
        string grupo = ObtenerGrupoUsuario();
        Ejecutar("pkexec", $"chgrp {grupo} {destino.MountPoint}");
        Ejecutar("pkexec", $"chmod 770 {destino.MountPoint}");
        Ejecutar("pkexec", $"chmod g+s {destino.MountPoint}");

        destino.Conectado = true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al conectar destino {destino.Iqn}: {ex.Message}");
    }
}
 
    

    public static void Desconectar(IscsiDestino destino)
    {
        try
        {
            if (!string.IsNullOrEmpty(destino.MountPoint))
                Ejecutar("pkexec", $"umount {destino.MountPoint}");

            Ejecutar("pkexec", $"iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --logout");

            destino.Conectado = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al desconectar destino {destino.Iqn}: {ex.Message}");
        }
    }
    
    
    private static string ObtenerGrupoUsuario()
    {
        var grupo = Ejecutar("id", "-gn").Trim();
        return string.IsNullOrWhiteSpace(grupo) ? "users" : grupo;
    }

}*/


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using ISCSI_Util.Models;
using ISCSI_Util.Utils;

namespace ISCSI_Util.Helpers;

public static class IscsiHelper
{
    ///////////////////////////////////////////////////////////////
    #region Descubrir_Destinos

    public static List<IscsiDestino> Descubrir(string ip)
    {
        var destinos = new List<IscsiDestino>();

        try
        {
            // 1. Discovery de IQN en el portal usando sudo -S
            string output = Ejecutar("sudo", $"-S iscsiadm -m discovery -t sendtargets -p {ip}");
            string sesionesOut = Ejecutar("sudo", "-S iscsiadm -m session");

            var sesiones = string.IsNullOrWhiteSpace(sesionesOut)
                ? Array.Empty<string>()
                : sesionesOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                // Buscar token que empiece por "iqn."
                string iqn = tokens.FirstOrDefault(t => t.StartsWith("iqn."));
                if (string.IsNullOrEmpty(iqn)) continue;

                bool conectado = sesiones.Any(s => s.Contains(iqn));

                // Evitar duplicados
                if (destinos.Any(d => d.Iqn == iqn && d.Ip == ip))
                    continue;

                var destino = new IscsiDestino
                {
                    Ip = ip,
                    Iqn = iqn,
                    Conectado = conectado,
                    Seleccionado = false
                };

                // Intentar localizar symlink en /dev/disk/by-path/
                var byPath = Ejecutar("ls", "-1 /dev/disk/by-path/");
                foreach (var dev in byPath.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (dev.Contains(iqn) && dev.Contains("lun"))
                    {
                        destino.DevicePath = Path.Combine("/dev/disk/by-path", dev.Trim());
                        break;
                    }
                }

                destinos.Add(destino);
                
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descubrir destinos: {ex.Message}");
        }

        NotificadorLinux.Enviar($"Se descubrieron {destinos.Count} destinos.");
        return destinos;
    }

    // Helper genérico que inyecta la contraseña guardada
    
    private static string Ejecutar(string fileName, string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();

        // Inyectar contraseña solo si es sudo -S
        if (fileName == "sudo" && args.Contains("-S") && !string.IsNullOrEmpty(Credenciales.AdminPassword))
        {
            // Escribir la contraseña seguida de salto de línea
            process.StandardInput.Write(Credenciales.AdminPassword + "\n");
            process.StandardInput.Flush();
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(error))
            Console.WriteLine($"Error ejecutando {fileName}: {error}");

        return output;
    }

    
    #endregion
    ///////////////////////////////////////////////////////////////

    
    
    public static void Conectar(IscsiDestino destino)
{
    try
    {
        // 0. Verificar si ya hay sesión activa
        var sesionesOut = Ejecutar("sudo", "-S iscsiadm -m session");
        bool yaConectado = sesionesOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                      .Any(s => s.Contains(destino.Iqn));

        if (!yaConectado)
        {
            // 1. Configurar CHAP si el destino lo requiere
            if (destino.UsaChap)
            {
                Ejecutar("sudo", $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --op=update --name node.session.auth.authmethod --value=CHAP");
                Ejecutar("sudo", $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --op=update --name node.session.auth.username --value={destino.UsuarioChap}");
                Ejecutar("sudo", $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --op=update --name node.session.auth.password --value={destino.PasswordChap}");
            }

            // 2. Login al destino
            Ejecutar("sudo", $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --login");
        }

        // 3. Crear carpeta de montaje
        destino.MountPoint = $"/mnt/iscsi/{FileSystemUtils.SanitizarNombre(destino.Iqn)}";
        Ejecutar("sudo", $"-S mkdir -p {destino.MountPoint}");

        // 4. Buscar symlink real en /dev/disk/by-path
        var output = Ejecutar("ls", "-1 /dev/disk/by-path/");
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains(destino.Ip) && line.Contains("lun"))
            {
                destino.DevicePath = "/dev/disk/by-path/" + line.Trim();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(destino.DevicePath))
            throw new InvalidOperationException($"No se encontró symlink para el destino {destino.Iqn} (IP {destino.Ip}).");

        // 5. Buscar partición
        var lsblkOut = Ejecutar("lsblk", "-rno NAME " + destino.DevicePath);
        var lines = lsblkOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
        {
            destino.DevicePath = "/dev/" + lines[1].Trim(); // primera partición
        }

        // 6. Detectar filesystem
        var blkidOut = Ejecutar("blkid", destino.DevicePath);
        string fsType = "ext4"; // fallback
        if (blkidOut.Contains("TYPE=\"ext2\"")) fsType = "ext2";
        else if (blkidOut.Contains("TYPE=\"ext3\"")) fsType = "ext3";
        else if (blkidOut.Contains("TYPE=\"ext4\"")) fsType = "ext4";
        else if (blkidOut.Contains("TYPE=\"xfs\"")) fsType = "xfs";
        else if (blkidOut.Contains("TYPE=\"btrfs\"")) fsType = "btrfs";
        else if (blkidOut.Contains("TYPE=\"f2fs\"")) fsType = "f2fs";
        else if (blkidOut.Contains("TYPE=\"ntfs\"")) fsType = "ntfs";
        else if (blkidOut.Contains("TYPE=\"vfat\"")) fsType = "vfat";
        else if (blkidOut.Contains("TYPE=\"exfat\"")) fsType = "exfat";
        else if (blkidOut.Contains("TYPE=\"iso9660\"")) fsType = "iso9660";

        // 7. Montar el dispositivo
        Ejecutar("sudo", $"-S mount -t {fsType} {destino.DevicePath} {destino.MountPoint}");

        // 8. Ajustar grupo y permisos
        string grupo = ObtenerGrupoUsuario();
        Ejecutar("sudo", $"-S chgrp {grupo} {destino.MountPoint}");
        Ejecutar("sudo", $"-S chmod 770 {destino.MountPoint}");
        Ejecutar("sudo", $"-S chmod g+s {destino.MountPoint}");

        destino.Conectado = true;
        NotificadorLinux.Enviar($"Destino {destino.Iqn} conectado en {destino.MountPoint}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al conectar destino {destino.Iqn}: {ex.Message}");
    }
}

    
    
    
 /* 
    public static void Conectar(IscsiDestino destino)
{
    try
    {
        // 0. Verificar si ya hay sesión activa
        var sesionesOut = Ejecutar("sudo", "-S iscsiadm -m session");
        bool yaConectado = sesionesOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                      .Any(s => s.Contains(destino.Iqn));

        if (!yaConectado)
        {
            // 1. Login al destino
            Ejecutar("sudo", $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --login");
        }

        // 2. Crear carpeta de montaje
        destino.MountPoint = $"/mnt/iscsi/{FileSystemUtils.SanitizarNombre(destino.Iqn)}";
        Ejecutar("sudo", $"-S mkdir -p {destino.MountPoint}");

        // 3. Buscar symlink real en /dev/disk/by-path
        var output = Ejecutar("ls", "-1 /dev/disk/by-path/");
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Ajuste: buscar por IP y lun, no solo por IQN
            if (line.Contains(destino.Ip) && line.Contains("lun"))
            {
                destino.DevicePath = "/dev/disk/by-path/" + line.Trim();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(destino.DevicePath))
            throw new InvalidOperationException($"No se encontró symlink para el destino {destino.Iqn} (IP {destino.Ip}).");

        // 4. Buscar partición
        var lsblkOut = Ejecutar("lsblk", "-rno NAME " + destino.DevicePath);
        var lines = lsblkOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
        {
            destino.DevicePath = "/dev/" + lines[1].Trim(); // primera partición
        }

        // 5. Detectar filesystem
        var blkidOut = Ejecutar("blkid", destino.DevicePath);
        string fsType = "ext4"; // fallback
        if (blkidOut.Contains("TYPE=\"ext2\"")) fsType = "ext2";
        else if (blkidOut.Contains("TYPE=\"ext3\"")) fsType = "ext3";
        else if (blkidOut.Contains("TYPE=\"ext4\"")) fsType = "ext4";
        else if (blkidOut.Contains("TYPE=\"xfs\"")) fsType = "xfs";
        else if (blkidOut.Contains("TYPE=\"btrfs\"")) fsType = "btrfs";
        else if (blkidOut.Contains("TYPE=\"f2fs\"")) fsType = "f2fs";
        else if (blkidOut.Contains("TYPE=\"ntfs\"")) fsType = "ntfs";
        else if (blkidOut.Contains("TYPE=\"vfat\"")) fsType = "vfat";
        else if (blkidOut.Contains("TYPE=\"exfat\"")) fsType = "exfat";
        else if (blkidOut.Contains("TYPE=\"iso9660\"")) fsType = "iso9660";

        // 6. Montar el dispositivo
        Ejecutar("sudo", $"-S mount -t {fsType} {destino.DevicePath} {destino.MountPoint}");

        // 7. Ajustar grupo y permisos
        string grupo = ObtenerGrupoUsuario();
        Ejecutar("sudo", $"-S chgrp {grupo} {destino.MountPoint}");
        Ejecutar("sudo", $"-S chmod 770 {destino.MountPoint}");
        Ejecutar("sudo", $"-S chmod g+s {destino.MountPoint}");

        destino.Conectado = true;
        NotificadorLinux.Enviar($"Destino {destino.Iqn} conectado en {destino.MountPoint}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al conectar destino {destino.Iqn}: {ex.Message}");
        
    }
}*/

    
    public static void Desconectar(IscsiDestino destino)
    {
        try
        {
            if (!string.IsNullOrEmpty(destino.MountPoint))
                Ejecutar("sudo", $"-S umount {destino.MountPoint}");

            Ejecutar("sudo", $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --logout");

            destino.Conectado = false;
            NotificadorLinux.Enviar($"Destino {destino.Iqn} desconectado");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al desconectar destino {destino.Iqn}: {ex.Message}");
        }
    }

   
    
        private static string ObtenerGrupoUsuario()
        {
            // Ejecuta 'id -gn' para obtener el grupo principal del usuario actual
            var grupo = Ejecutar("id", "-gn").Trim();
            return string.IsNullOrWhiteSpace(grupo) ? "users" : grupo;
        }
    }
