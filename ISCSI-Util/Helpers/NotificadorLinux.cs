using System;
using System.Diagnostics;

namespace ISCSI_Util.Helpers;


    
    public static class NotificadorLinux
    {
        public static void Enviar(string mensaje)
        {
            try
            {
                Process.Start("notify-send", $"\"ISCSI Util\" \"{mensaje}\"");
            }
            catch
            {
                // Si notify-send no está disponible, fallback a consola
                Console.WriteLine($"[NOTIFICACIÓN] {mensaje}");
            }
        }
    }
    
    
