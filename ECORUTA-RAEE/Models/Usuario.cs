using System;
using System.Collections.Generic;
using System.Text;

namespace ECORUTA_RAEE.Models;

public class Usuario
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public string Rol { get; set; } = "Ciudadano";
}
