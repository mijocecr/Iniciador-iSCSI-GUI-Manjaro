

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
    #region Descubrir_Destinos

    public static List<IscsiDestino> Descubrir(string ip)
    {
        var destinos = new List<IscsiDestino>();
        try
        {
            string output = Ejecutar("sudo", $"-S iscsiadm -m discovery -t sendtargets -p {ip}");
            string sesionesOut = Ejecutar("sudo", "-S iscsiadm -m session");

            var sesiones = string.IsNullOrWhiteSpace(sesionesOut)
                ? Array.Empty<string>()
                : sesionesOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var byPath = Ejecutar("ls", "-1 /dev/disk/by-path/")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                string iqn = tokens.LastOrDefault(t => t.StartsWith("iqn."));
                if (string.IsNullOrEmpty(iqn)) continue;

                bool conectado = sesiones.Any(s => s.Contains(iqn));

                if (destinos.Any(d => d.Iqn == iqn && d.Ip == ip))
                    continue;

                var destino = new IscsiDestino
                {
                    Ip = ip,
                    Iqn = iqn,
                    Conectado = conectado,
                    Seleccionado = false
                };

                destino.DevicePath = byPath.FirstOrDefault(dev => dev.Contains(ip) && dev.Contains("lun"))
                    ?.Trim();

                if (!string.IsNullOrEmpty(destino.DevicePath))
                    destino.DevicePath = Path.Combine("/dev/disk/by-path", destino.DevicePath);

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

    #endregion

    // Helpers
  
    
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
    
    
    private static int EjecutarConCodigo(string fileName, string args)
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
            process.StandardInput.Write(Credenciales.AdminPassword + "\n");
            process.StandardInput.Flush();
        }

        process.WaitForExit();
        return process.ExitCode;
    }


    private static string ObtenerGrupoUsuario()
    {
        var grupo = Ejecutar("id", "-gn").Trim();
        return string.IsNullOrWhiteSpace(grupo) ? "users" : grupo;
    }

    private static string DetectarFsType(string blkidOut)
    {
        if (blkidOut.Contains("TYPE=\"ext2\"")) return "ext2";
        if (blkidOut.Contains("TYPE=\"ext3\"")) return "ext3";
        if (blkidOut.Contains("TYPE=\"ext4\"")) return "ext4";
        if (blkidOut.Contains("TYPE=\"xfs\"")) return "xfs";
        if (blkidOut.Contains("TYPE=\"btrfs\"")) return "btrfs";
        if (blkidOut.Contains("TYPE=\"f2fs\"")) return "f2fs";
        if (blkidOut.Contains("TYPE=\"ntfs\"")) return "ntfs";
        if (blkidOut.Contains("TYPE=\"vfat\"")) return "vfat";
        if (blkidOut.Contains("TYPE=\"exfat\"")) return "exfat";
        if (blkidOut.Contains("TYPE=\"iso9660\"")) return "iso9660";
        return "ext4"; // fallback
    }

    // Conectar
    public static void Conectar(IscsiDestino destino)
    {
        try
        {
            var sesionesOut = Ejecutar("sudo", "-S iscsiadm -m session");
            bool yaConectado = sesionesOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Any(s => s.Contains(destino.Iqn));

            if (!yaConectado)
            {
                if (destino.UsaChap)
                {
                    Ejecutar("sudo",
                        $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --op=update --name node.session.auth.authmethod --value=CHAP");
                    Ejecutar("sudo",
                        $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --op=update --name node.session.auth.username --value={destino.UsuarioChap}");
                    Ejecutar("sudo",
                        $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --op=update --name node.session.auth.password --value={destino.PasswordChap}");
                }

                Ejecutar("sudo", $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --login");
            }

            destino.MountPoint = $"/mnt/iscsi/{FileSystemUtils.SanitizarNombre(destino.Iqn)}";
            Ejecutar("sudo", $"-S mkdir -p {destino.MountPoint}");

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
                throw new InvalidOperationException($"No se encontró symlink para {destino.Iqn} (IP {destino.Ip}).");

            var lsblkOut = Ejecutar("lsblk", "-rno NAME " + destino.DevicePath);
            var lines = lsblkOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1)
                destino.PartitionPath = "/dev/" + lines[1].Trim();
            else
                destino.PartitionPath = destino.DevicePath;

            var blkidOut = Ejecutar("blkid", destino.PartitionPath);
            string fsType = DetectarFsType(blkidOut);

            Ejecutar("sudo", $"-S mount -t {fsType} {destino.PartitionPath} {destino.MountPoint}");

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

  
 
   
    
    
    // Los demás métodos (Desconectar, CrearServicioPersistencia, Eliminar

    // Desconectar
  
    
    public static void Desconectar(IscsiDestino destino, bool eliminarPersistencia = false)
{
    try
    {
        // 1. Desmontar solo si está montado
        if (!string.IsNullOrEmpty(destino.MountPoint))
        {
            int rcMount = EjecutarConCodigo("mountpoint", $"-q {destino.MountPoint}");
            if (rcMount == 0) // 0 = está montado
            {
                NotificadorLinux.Enviar($"Desmontando {destino.MountPoint}...");
                Ejecutar("sudo", $"-S umount " + destino.MountPoint);
            }
            else
            {
                NotificadorLinux.Enviar($"{destino.MountPoint} ya estaba desmontado.");
            }
        }

        // 2. Logout solo si la sesión sigue activa
        var sesionesOut = Ejecutar("sudo", "-S iscsiadm -m session");
        bool conectado = sesionesOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                    .Any(s => s.Contains(destino.Iqn));

        if (conectado)
        {
            NotificadorLinux.Enviar($"Cerrando sesión iSCSI para {destino.Iqn}...");
            Ejecutar("sudo", $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --logout");
        }
        else
        {
            NotificadorLinux.Enviar($"La sesión iSCSI {destino.Iqn} ya estaba cerrada.");
        }

        destino.Conectado = false;

        // 3. Si se pide eliminar persistencia, limpiar servicio + script + fstab
        if (eliminarPersistencia)
        {
            string safeName = FileSystemUtils.SanitizarNombre(destino.Iqn);
            string serviceName = $"iscsi-{safeName}.service";
            string servicePath = $"/etc/systemd/system/{serviceName}";
            string scriptPath = $"/usr/local/bin/mount-iscsi-{safeName}.sh";

            Ejecutar("sudo", $"-S systemctl disable " + serviceName);
            Ejecutar("sudo", $"-S rm -f " + servicePath);
            Ejecutar("sudo", $"-S rm -f " + scriptPath);
            Ejecutar("sudo", "-S systemctl daemon-reload");

            // 🔒 Eliminar entrada en fstab de forma segura
            Ejecutar("sudo", "-S cp /etc/fstab /etc/fstab.bak"); // backup
            Ejecutar("sudo", $"-S sed -i '/{destino.MountPoint}/d' /etc/fstab");
            Ejecutar("sudo", "-S mount -a"); // validar que sigue siendo correcto

            // Marcar nodo como manual
            Ejecutar("sudo", 
                $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --op update --name node.startup --value manual");

            NotificadorLinux.Enviar($"Persistencia eliminada para {destino.Iqn}");
        }

        NotificadorLinux.Enviar($"Destino {destino.Iqn} desconectado correctamente.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al desconectar destino {destino.Iqn}: {ex.Message}");
    }
}

  
 
    public static void ConfigurarPersistencia(IscsiDestino destino, string fsType)
    {
        try
        {
            // 1. Marcar nodo como automático
            Ejecutar("sudo",
                $"-S iscsiadm -m node -T {destino.Iqn} -p {destino.Ip} --op update --name node.startup --value automatic");

            // 2. Obtener UUID de la partición exacta (parseo robusto)
            var blkidOut = Ejecutar("sudo", $"-S blkid {destino.PartitionPath}");
            string uuid = blkidOut.Split(' ')
                .FirstOrDefault(s => s.StartsWith("UUID="))?
                .Replace("UUID=", "")
                .Trim('"');

            if (string.IsNullOrEmpty(uuid))
                throw new Exception($"No se pudo obtener UUID para {destino.PartitionPath}");

            // 3. Comprobar si ya existe en /etc/fstab por UUID o MountPoint (sin sudo, grep puede leer fstab)
            var checkUuid = Ejecutar("grep", $"-q '{uuid}' /etc/fstab && echo exists");
            var checkMount = Ejecutar("grep", $"-q '{destino.MountPoint}' /etc/fstab && echo exists");

            if (!checkUuid.Contains("exists") && !checkMount.Contains("exists"))
            {
                // 4. Backup antes de modificar
                Ejecutar("sudo", "-S cp /etc/fstab /etc/fstab.bak");

                // 5. Añadir entrada solo si no existe (usando espacios, no tabs)
                string fstabEntry = $"UUID={uuid} {destino.MountPoint} {fsType} defaults,_netdev 0 0";
                Ejecutar("sudo", $"-S bash -c \"echo '{fstabEntry}' | tee -a /etc/fstab\"");

                // 6. Validar que fstab sigue siendo correcto
                Ejecutar("sudo", "-S mount -a");

                NotificadorLinux.Enviar($"Persistencia configurada para {destino.Iqn} en {destino.MountPoint}");
            }
            else
            {
                NotificadorLinux.Enviar($"El destino {destino.Iqn} ya estaba configurado en /etc/fstab");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al configurar persistencia para {destino.Iqn}: {ex.Message}");
        }
    }

    
    
    
    
// CrearServicioPersistencia
   

public static void CrearServicioPersistencia(IscsiDestino destino)
{
    try
    {
        // Usar IQN completo en el nombre del servicio/script
        string rawServiceName = $"iscsi-{destino.Iqn}.service";
        string servicePath = $"/etc/systemd/system/{rawServiceName}";
        string scriptPath = $"/usr/local/bin/mount-iscsi-{destino.Iqn}.sh";

        // 1. Script robusto (solo si no existe)
        var scriptExists = Ejecutar("sudo", $"-S bash -c \"test -f '{scriptPath}' && echo exists\"");
        if (!scriptExists.Contains("exists"))
        {
            string scriptContent = $@"#!/bin/bash
TARGET=""{destino.Iqn}""
PORTAL=""{destino.Ip}""
MOUNTPOINT=""{destino.MountPoint}""

# Login si no hay sesión activa
if ! iscsiadm -m session | grep -q ""$TARGET""; then
  iscsiadm -m node -T ""$TARGET"" -p ""$PORTAL"" --login
  for i in {{1..10}}; do
    if ls /dev/disk/by-path/*""$TARGET""*lun* &>/dev/null; then
      break
    fi
    sleep 1
  done
fi

# Montar si no está montado
if ! mountpoint -q ""$MOUNTPOINT""; then
  mount ""$MOUNTPOINT""
fi

exit 0
";
            // Crear script como root
            Ejecutar("sudo", $"-S bash -c \"cat > '{scriptPath}' <<'EOF'\n{scriptContent}\nEOF\"");
            Ejecutar("sudo", $"-S chmod +x '{scriptPath}'");
        }

        // 2. Servicio systemd (solo si no existe)
        var serviceExists = Ejecutar("sudo", $"-S bash -c \"test -f '{servicePath}' && echo exists\"");
        if (!serviceExists.Contains("exists"))
        {
            string serviceContent = $@"
[Unit]
Description=Conectar iSCSI y montar {destino.Iqn}
After=network-online.target iscsid.service
Requires=network-online.target iscsid.service

[Service]
Type=oneshot
ExecStart={scriptPath}
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
";
            Ejecutar("sudo", $"-S bash -c \"cat > '{servicePath}' <<'EOF'\n{serviceContent}\nEOF\"");
        }

        // 3. Recargar systemd y habilitar
        Ejecutar("sudo", "-S systemctl daemon-reload");
        Ejecutar("sudo", $"-S systemctl enable '{rawServiceName}'");

        NotificadorLinux.Enviar($"Servicio {rawServiceName} creado y habilitado para {destino.Iqn}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al crear servicio persistente para {destino.Iqn}: {ex.Message}");
    }
}




// EliminarServicioPersistencia

    public static void EliminarServicioPersistencia(IscsiDestino destino)
    {
        try
        {
            string rawServiceName = $"iscsi-{destino.Iqn}.service";
            string servicePath = $"/etc/systemd/system/{rawServiceName}";
            string scriptPath = $"/usr/local/bin/mount-iscsi-{destino.Iqn}.sh";

            // 1. Deshabilitar y eliminar servicio + script
            Ejecutar("sudo", $"-S systemctl disable " + rawServiceName);

            Ejecutar("sudo", $"-S bash -c \"rm -f '{servicePath}'\"");
            Ejecutar("sudo", $"-S bash -c \"rm -f '{scriptPath}'\"");
            Ejecutar("sudo", "-S systemctl daemon-reload");

            // 2. Eliminar entrada en fstab de forma segura
            Ejecutar("sudo", "-S cp /etc/fstab /etc/fstab.bak"); // backup
            Ejecutar("sudo", $"-S bash -c \"sed -i '\\|{destino.MountPoint}|d' /etc/fstab\"");
            Ejecutar("sudo", "-S mount -a"); // validar que sigue siendo correcto

            // 3. Marcar nodo como manual
            Ejecutar("sudo",
                $"-S iscsiadm -m node -T '{destino.Iqn}' -p {destino.Ip} --op update --name node.startup --value manual");

            NotificadorLinux.Enviar($"Persistencia eliminada para {destino.Iqn}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al eliminar servicio persistente para {destino.Iqn}: {ex.Message}");
        }
    }



}

