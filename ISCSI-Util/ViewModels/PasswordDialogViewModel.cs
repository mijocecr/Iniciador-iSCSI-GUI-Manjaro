using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ISCSI_Util.ViewModels
{
    public class PasswordDialogViewModel : ObservableObject
    {
        private string _password;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public RelayCommand AceptarCommand { get; }

        public PasswordDialogViewModel(Action<string> onPasswordEntered)
        {
            AceptarCommand = new RelayCommand(() =>
            {
                // Solo devolver la contraseña al callback
                onPasswordEntered?.Invoke(Password);
            });
        }
    }
}