/*using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ISCSI_Util.Helpers;
using ISCSI_Util.Models;
using ISCSI_Util.Utils;

namespace ISCSI_Util.ViewModels;

public partial class MainWindowViewModel: ObservableObject
{
    
   
    public ObservableCollection<IscsiDestino> Destinos { get; } = new();

    [ObservableProperty] private string usuario;
    [ObservableProperty] private string password;
    
    private string _ipServidor;
    public string IpServidor
    {
        get => _ipServidor;
        set => SetProperty(ref _ipServidor, value);
    }

    
    
    
    [RelayCommand]
    public void DescubrirDestinos()
    {
        Destinos.Clear();

        if (string.IsNullOrWhiteSpace(IpServidor))
        {
            Console.WriteLine("No se indicó IP.");
            return;
        }

        var encontrados = IscsiHelper.Descubrir(IpServidor);

        foreach (var destino in encontrados)
            Destinos.Add(destino);

        Console.WriteLine($"Se descubrieron {Destinos.Count} destinos.");
    }



    [RelayCommand]
    private void ConectarSeleccionados()
    {
        foreach (var destino in Destinos.Where(d => d.Seleccionado))
        {
            IscsiHelper.Conectar(destino);
        }
    }

    [RelayCommand]
    private void DesconectarSeleccionados()
    {
        foreach (var destino in Destinos.Where(d => d.Seleccionado))
        {
            IscsiHelper.Desconectar(destino);
        }
    }
    
    
}*/


using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ISCSI_Util.Helpers;
using ISCSI_Util.Models;

namespace ISCSI_Util.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<IscsiDestino> Destinos { get; } = new();

    [ObservableProperty] private string usuario;
    [ObservableProperty] private string password;

    private string _ipServidor;
    public string IpServidor
    {
        get => _ipServidor;
        set => SetProperty(ref _ipServidor, value);
    }

    // Comando asíncrono
    [RelayCommand]
    public async Task DescubrirDestinosAsync()
    {
        // 1. Pedir contraseña si no está guardada
        await EnsurePasswordAsync();

        Destinos.Clear();

        if (string.IsNullOrWhiteSpace(IpServidor))
        {
            Console.WriteLine("No se indicó IP.");
            return;
        }

        // 2. Descubrir destinos
        var encontrados = IscsiHelper.Descubrir(IpServidor);

        foreach (var destino in encontrados)
            Destinos.Add(destino);

        Console.WriteLine($"Se descubrieron {Destinos.Count} destinos.");
    }

    [RelayCommand]
    private void ConectarSeleccionados()
    {
        foreach (var destino in Destinos.Where(d => d.Seleccionado))
        {
            IscsiHelper.Conectar(destino);
        }
    }

    [RelayCommand]
    private void DesconectarSeleccionados()
    {
        foreach (var destino in Destinos.Where(d => d.Seleccionado))
        {
            IscsiHelper.Desconectar(destino);
        }
    }

    // Método auxiliar para abrir el PasswordDialog
    private async Task EnsurePasswordAsync()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is ISCSI_Util.Views.MainWindow mw)
        {
            await mw.SolicitarPassword();
        }
    }
}
