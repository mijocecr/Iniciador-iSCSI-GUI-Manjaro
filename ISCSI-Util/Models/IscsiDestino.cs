using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ISCSI_Util.Models;

public class IscsiDestino : INotifyPropertyChanged
{
    private bool _conectado;
    private bool _seleccionado;
    private string _devicePath;
    private string _mountPoint;
    private string _ip;
    private string _iqn;
    
    
    // Campos para CHAP
    public bool UsaChap { get; set; } = false;
    public string UsuarioChap { get; set; }
    public string PasswordChap { get; set; }
    

    public string Ip
    {
        get => _ip;
        set { if (_ip != value) { _ip = value; OnPropertyChanged(); } }
    }

    public string Iqn
    {
        get => _iqn;
        set { if (_iqn != value) { _iqn = value; OnPropertyChanged(); } }
    }

    public string DevicePath
    {
        get => _devicePath;
        set { if (_devicePath != value) { _devicePath = value; OnPropertyChanged(); } }
    }

    public string MountPoint
    {
        get => _mountPoint;
        set { if (_mountPoint != value) { _mountPoint = value; OnPropertyChanged(); } }
    }

    public bool Conectado
    {
        get => _conectado;
        set { if (_conectado != value) { _conectado = value; OnPropertyChanged(); } }
    }

    public bool Seleccionado
    {
        get => _seleccionado;
        set { if (_seleccionado != value) { _seleccionado = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}