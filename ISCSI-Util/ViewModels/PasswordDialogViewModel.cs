

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ISCSI_Util.Utils;

namespace ISCSI_Util.ViewModels
{
    public class PasswordDialogViewModel:ObservableObject
    {
        public string Password { get; set; }

        public RelayCommand AceptarCommand { get; }

        public PasswordDialogViewModel(Action<string> onPasswordEntered)
        {
            // Versión sin parámetros
            AceptarCommand = new RelayCommand(() =>
            {
                Credenciales.AdminPassword = Password;
                onPasswordEntered?.Invoke(Password);
            });
        }
    }
}
