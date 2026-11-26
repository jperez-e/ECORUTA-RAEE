namespace ECORUTA_RAEE.Views;

internal interface IFormularioDemostracion
{
    bool Ocupado { get; }
    bool HayBorrador { get; }
    bool SalidaConfirmada { get; }
}
