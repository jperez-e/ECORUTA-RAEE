
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.ViewModels;

namespace ECORUTA_RAEE.Views;

public partial class SolicitudFormPage : ContentPage, IQueryAttributable
{
    private readonly SolicitudFormViewModel _viewModel;

    public SolicitudFormPage()
    {
        InitializeComponent();

        BindingContext = _viewModel = new SolicitudFormViewModel();

        // Por defecto, formulario nuevo con peso manual habilitado.
        _viewModel.PrepararNuevaSolicitud();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Solicitud", out object? valor)
            && valor is SolicitudRecoleccion solicitud)
        {
            // Si se abre para editar, cargar la solicitud existente.
            _viewModel.CargarParaEditar(solicitud);
        }
    }
}