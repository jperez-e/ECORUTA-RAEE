using Microsoft.Maui.ApplicationModel.Communication;
using ECORUTA_RAEE.Models;
using ECORUTA_RAEE.Services;
using ECORUTA_RAEE.ViewModels;
using static ECORUTA_RAEE.Views.TarjetasDemo;

namespace ECORUTA_RAEE.Views;

internal sealed class DetalleCampoPage : ContentPage
{
    private readonly DetalleCampoViewModel _vm;
    private readonly VerticalStackLayout _contenido = new() { Padding = 16, Spacing = 10 };
    private readonly ScrollView _scroll;
    private readonly Label _aviso = Texto("Cargando jornada…");
    private bool _ocupada;

    private DetalleCampoPage(int jornadaId, int? solicitudId = null)
    {
        _vm = new(jornadaId, solicitudId);
        BindingContext = _vm;
        Title = solicitudId.HasValue ? "Detalle de parada" : "Detalle de jornada";
        Style = Estilo("CanvasVisual");
        _contenido.Add(Boton("Volver", () => Navigation.PopModalAsync()));
        _contenido.Add(_aviso);
        Content = _scroll = new ScrollView { Content = _contenido };
    }

    internal static DetalleCampoPage ParaJornada(int id) => new(id);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EjecutarConConsultaAsync();
    }

    private async Task EjecutarConConsultaAsync(Func<ConsultaCampo, Task>? accion = null, bool conservarPosicion = true)
    {
        if (_ocupada || SesionDemostracion.Perfil != PerfilDemostracion.Recolector)
            return;

        _ocupada = true;
        try
        {
            var anterior = _vm.Vista;
            var resultado = await _vm.ActualizarAsync();
            if (!resultado.Correcto || resultado.Datos is null)
            {
                MostrarAviso(resultado.Correcto
                    ? "La jornada ya no está programada o la parada dejó de pertenecerle. Vuelve al listado."
                    : "No se pudo actualizar la jornada. Reintenta antes de continuar.");
                return;
            }

            var consulta = resultado.Datos;
            bool cambio = !DetalleCampoViewModel.Equivalentes(anterior, consulta);
            if (cambio)
            {
                double posicion = _scroll.ScrollY;
                Mostrar(consulta);
                await _scroll.ScrollToAsync(0, conservarPosicion ? posicion : 0, false);
            }
            else
                _aviso.IsVisible = false;

            if (accion is not null)
            {
                if (cambio)
                    MostrarAviso("Los datos cambiaron. Revisa la información actualizada y vuelve a pulsar la acción.");
                else
                    await accion(consulta);
            }
        }
        finally { _ocupada = false; }
    }

    private void MostrarAviso(string mensaje)
    {
        _aviso.Text = mensaje;
        _aviso.IsVisible = true;
        if (!_contenido.Children.Contains(_aviso))
            _contenido.Insert(1, _aviso);
        if (!_contenido.Children.OfType<Button>().Any(b => b.Text == "Actualizar"))
            _contenido.Insert(2, Boton("Actualizar", () => EjecutarConConsultaAsync()));
    }

    private void Mostrar(ConsultaCampo consulta)
    {
        _contenido.Clear();
        _contenido.Add(Boton("Volver", () => Navigation.PopModalAsync()));
        _aviso.IsVisible = false;
        _contenido.Add(_aviso);
        if (_vm.SolicitudId is int id)
            MostrarParada(consulta, id);
        else
            MostrarJornada(consulta);
    }

    private void MostrarJornada(ConsultaCampo consulta)
    {
        var jornada = consulta.Resumen;
        _contenido.Add(Cabecera($"Recogida en {jornada.Zona}",
            $"{jornada.Fecha:d 'de' MMMM 'de' yyyy} · {jornada.Paradas} paradas · {ParadaCampo.UnidadesVisibles(jornada.Unidades)} · {jornada.Peso:0.##} kg"));
        _contenido.Add(EstadoVisual(jornada.EstadoOrden, consulta.OrdenVigente ? "verde" : "ambar"));
        _contenido.Add(Texto(consulta.OrdenVigente
            ? "Las distancias del mapa son aproximadas y en línea recta."
            : "El gestor debe revisar el orden. Las solicitudes se muestran por fecha de registro."));
        if (consulta.Paradas.Any(p => p.Solicitud.UbicacionPendiente))
            _contenido.Add(EstadoVisual("Hay ubicaciones por completar. Consulta las referencias y coordina con el gestor.", "ambar"));
        _contenido.Add(Boton("Ver mapa completo", () => EjecutarConConsultaAsync(_ =>
            Navigation.PushModalAsync(new MapaJornadaPage(jornada.Id, consultaDeCampo: true))), true));
        _contenido.Add(Texto(consulta.OrdenVigente ? "Orden de las paradas" : "Solicitudes de la jornada", "TituloVisual"));
        for (int i = 0; i < consulta.Paradas.Count; i++)
        {
            var parada = consulta.Paradas[i];
            int id = parada.Solicitud.Id;
            var codigo = Texto(parada.Solicitud.CodigoSolicitud, "TituloVisual");
            var titulo = consulta.OrdenVigente ? IndicadorParada(i + 1, codigo) : ConIcono("punto", codigo);
            _contenido.Add(Tarjeta(titulo,
                Texto(parada.Solicitud.DireccionReferencia),
                Texto($"{ParadaCampo.UnidadesVisibles(parada.Unidades)} · {parada.Solicitud.PesoTotalEstimado:0.##} kg"),
                Boton("Ver parada", () => EjecutarConConsultaAsync(vigente => vigente.Indice(id) < 0
                    ? Task.CompletedTask : Navigation.PushModalAsync(new DetalleCampoPage(jornada.Id, id))))));
        }
    }

    private static View IndicadorParada(int posicion, View codigo)
    {
        var numero = Texto(posicion.ToString(), "OnGreen");
        numero.HorizontalTextAlignment = TextAlignment.Center;
        numero.VerticalTextAlignment = TextAlignment.Center;
        var ficha = new Border
        {
            MinimumWidthRequest = 32, MinimumHeightRequest = 32, StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            BackgroundColor = ColorRecurso("PrimaryGreen"), Content = numero
        };
        var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 8 };
        fila.Add(ficha);
        fila.Add(codigo, 1, 0);
        return fila;
    }

    private void MostrarParada(ConsultaCampo consulta, int id)
    {
        int indice = consulta.Indice(id);
        var parada = consulta.Paradas[indice];
        var solicitud = parada.Solicitud;
        string titulo = consulta.OrdenVigente ? $"Parada {indice + 1} de {consulta.Paradas.Count}" : "Detalle de parada";
        _contenido.Add(Cabecera(titulo, $"Jornada · {consulta.Resumen.Zona} · {solicitud.CodigoSolicitud}"));
        if (!consulta.OrdenVigente)
            _contenido.Add(EstadoVisual("El gestor debe revisar el orden. Puedes consultar los datos de cada solicitud.", "ambar"));

        var contacto = new List<View> { ConIcono("persona", Texto("Contacto de recogida", "TituloVisual")),
            Texto(solicitud.NombreContacto ?? "Sin nombre registrado"),
            Texto(solicitud.TelefonoContacto ?? "Sin teléfono registrado") };
        if (Telefono(solicitud) is not null)
        {
            var acciones = DosColumnas();
            acciones.Add(Boton("Llamar", () => ConParadaAsync(s =>
                Telefono(s) is { } numero ? AbrirMarcadorAsync(numero) : Task.CompletedTask), true));
            acciones.Add(Boton("WhatsApp", () => ConParadaAsync(s =>
                Telefono(s) is { } numero ? AbrirWhatsAppAsync(numero, s.CodigoSolicitud) : Task.CompletedTask)), 1, 0);
            contacto.Add(acciones);
        }
        _contenido.Add(Tarjeta(contacto.ToArray()));

        var ubicacion = new List<View> { ConIcono("punto", Texto("Ubicación de recogida", "TituloVisual")),
            Texto(solicitud.Zona), Texto(solicitud.DireccionReferencia) };
        if (Coordenadas.SonValidas(solicitud))
        {
            ubicacion.Add(EstadoVisual("Punto de recogida confirmado", "verde"));
            var abrirMapa = Boton("Cómo llegar", () => ConParadaAsync(AbrirMapaExternoAsync), true);
            abrirMapa.BackgroundColor = ColorRecurso("StatusBlue");
            SemanticProperties.SetDescription(abrirMapa, "Cómo llegar. Abre el destino en la aplicación de mapas del dispositivo.");
            ubicacion.Add(abrirMapa);
        }
        else
            ubicacion.Add(EstadoVisual("Punto de recogida pendiente. Consulta la referencia y coordina con el gestor.", "ambar"));

        ubicacion.Add(Boton("Ver mapa interno", () => EjecutarConConsultaAsync(_ =>
            Navigation.PushModalAsync(new MapaJornadaPage(consulta.Resumen.Id, consultaDeCampo: true, paradaInicial: id)))));
        _contenido.Add(Tarjeta(ubicacion.ToArray()));

        var equipos = new List<View> { Texto("Equipos para recoger", "TituloVisual") };
        foreach (var equipo in consulta.Equipos)
            equipos.Add(ConIcono("equipo", Texto(
                $"{equipo.Categoria} · {equipo.Cantidad} {(equipo.Cantidad == 1 ? "unidad" : "unidades")} · {equipo.PesoEstimado:0.##} kg en total")));
        if (consulta.Equipos.Count == 0)
            equipos.Add(Texto("Sin equipos asociados."));
        equipos.Add(EstadoVisual($"{ParadaCampo.UnidadesVisibles(parada.Unidades)} · {solicitud.PesoTotalEstimado:0.##} kg estimados", "verde"));
        _contenido.Add(Tarjeta(equipos.ToArray()));

        var navegacion = DosColumnas();
        var anterior = Boton("Anterior", () => IrAdyacenteAsync(-1));
        var siguiente = Boton("Siguiente", () => IrAdyacenteAsync(1), true);
        anterior.IsEnabled = consulta.Adyacente(id, -1).HasValue;
        siguiente.IsEnabled = consulta.Adyacente(id, 1).HasValue;
        navegacion.Add(anterior);
        navegacion.Add(siguiente, 1, 0);
        _contenido.Add(navegacion);
        _contenido.Add(Texto("Consultar una parada no registra su recogida."));
    }

    private async Task IrAdyacenteAsync(int direccion)
    {
        int? destino = null;
        await EjecutarConConsultaAsync(vigente =>
        {
            if (_vm.SolicitudId is int id)
                destino = vigente.Adyacente(id, direccion);
            return Task.CompletedTask;
        });
        if (destino is int id)
        {
            _vm.Seleccionar(id);
            await EjecutarConConsultaAsync(conservarPosicion: false);
        }
    }

    private Task ConParadaAsync(Func<SolicitudRecoleccion, Task> accion) => EjecutarConConsultaAsync(datos =>
        _vm.SolicitudId is int id && datos.Indice(id) is var indice && indice >= 0
            ? accion(datos.Paradas[indice].Solicitud) : Task.CompletedTask);

    private static string? Telefono(SolicitudRecoleccion solicitud) =>
        DatosContacto.TryNormalizarTelefono(solicitud.TelefonoContacto, false, out var numero) ? numero : null;

    private static Grid DosColumnas() => new()
    {
        ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8
    };

    private async Task AbrirMapaExternoAsync(SolicitudRecoleccion solicitud)
    {
        if (!Coordenadas.SonValidas(solicitud))
            return;

        try
        {
#if ANDROID
            if (await AbrirMapasAndroidAsync(solicitud.Latitud!.Value, solicitud.Longitud!.Value))
                return;
#else
            if (await Microsoft.Maui.ApplicationModel.Map.Default.TryOpenAsync(
                solicitud.Latitud!.Value, solicitud.Longitud!.Value, new MapLaunchOptions { Name = "Punto de recogida" }))
                return;
#endif
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or InvalidOperationException or ArgumentException
#if ANDROID
            or Android.Content.ActivityNotFoundException or Java.Lang.SecurityException
#endif
            ) { }

        await DisplayAlertAsync("Mapas no disponible", "Consulta el mapa interno o utiliza la referencia de recogida.", "Entendido");
    }

#if ANDROID
    // Algunos receptores de geo: no son mapas. Solo se entrega el punto a un receptor de cartografía.
    private static readonly HashSet<string> MapasAndroidReconocidos = new(StringComparer.Ordinal)
    {
        "com.google.android.apps.maps", "com.waze", "net.osmand", "net.osmand.plus",
        "app.organicmaps", "com.mapswithme.maps.pro", "com.here.app.maps", "com.mapfactor.navigator"
    };

    private async Task<bool> AbrirMapasAndroidAsync(double latitud, double longitud)
    {
        var actividad = Platform.CurrentActivity;
        var paquetes = actividad?.PackageManager;
        if (actividad is null || paquetes is null)
            return false;

        using var uri = Android.Net.Uri.Parse(FormattableString.Invariant($"geo:{latitud:R},{longitud:R}?q={latitud:R},{longitud:R}"));
        using var destino = new Android.Content.Intent(Android.Content.Intent.ActionView, uri);
        using var categoria = new Android.Content.Intent(Android.Content.Intent.ActionMain);
        categoria.AddCategory(Android.Content.Intent.CategoryAppMaps);
        var declarados = ConsultarAplicaciones(paquetes, categoria, 0)
            .Select(r => r.ActivityInfo?.PackageName).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var opciones = ConsultarAplicaciones(paquetes, destino, Android.Content.PM.PackageInfoFlags.MatchDefaultOnly)
            .Where(r => r.ActivityInfo?.PackageName is { } id &&
                (declarados.Contains(id) || MapasAndroidReconocidos.Contains(id)))
            .DistinctBy(r => r.ActivityInfo!.PackageName, StringComparer.Ordinal)
            .Select(r => (Paquete: r.ActivityInfo!.PackageName!, Nombre: r.LoadLabel(paquetes) ?? r.ActivityInfo!.PackageName!))
            .ToArray();
        if (opciones.Length == 0)
            return false;

        string paquete = opciones[0].Paquete;
        if (opciones.Length > 1)
        {
            var nombres = opciones.Select(o => opciones.Count(a => a.Nombre == o.Nombre) > 1
                ? $"{o.Nombre} ({o.Paquete})" : o.Nombre).ToArray();
            string? elegida = await DisplayActionSheetAsync("Abrir destino en mapas", "Cancelar", null, nombres);
            int indice = Array.IndexOf(nombres, elegida);
            if (indice < 0)
                return true;
            var consulta = await _vm.ConsultarAsync();
            if (consulta.Datos is not { } vigente || _vm.SolicitudId is not int id ||
                vigente.Paradas.FirstOrDefault(p => p.Solicitud.Id == id)?.Solicitud is not { } solicitud ||
                solicitud.Latitud != latitud || solicitud.Longitud != longitud)
            {
                MostrarAviso("Cambió la jornada o el destino. Actualiza la parada y vuelve a intentarlo.");
                return true;
            }
            paquete = opciones[indice].Paquete;
        }
        destino.SetPackage(paquete);
        actividad.StartActivity(destino);
        return true;
    }

    private static IList<Android.Content.PM.ResolveInfo> ConsultarAplicaciones(Android.Content.PM.PackageManager paquetes,
        Android.Content.Intent intent, Android.Content.PM.PackageInfoFlags flags)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            return paquetes.QueryIntentActivities(intent, Android.Content.PM.PackageManager.ResolveInfoFlags.Of((long)flags)) ?? [];
#pragma warning disable CA1422 // Android anteriores a 33 requieren esta sobrecarga.
        return paquetes.QueryIntentActivities(intent, flags) ?? [];
#pragma warning restore CA1422
    }
#endif

    private async Task AbrirMarcadorAsync(string numero)
    {
        try
        {
            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(numero);
                return;
            }
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or InvalidOperationException or ArgumentException
#if ANDROID
            or Android.Content.ActivityNotFoundException or Java.Lang.SecurityException
#endif
            ) { }

        await DisplayAlertAsync("Marcador no disponible", "Puedes consultar el número en el detalle de la parada.", "Entendido");
    }

    private async Task AbrirWhatsAppAsync(string numero, string codigo)
    {
        var mensaje = $"Hola, soy parte del equipo de recogida de EcoRuta RAEE. Me comunico contigo para coordinar la entrega de los equipos electrónicos correspondientes a la solicitud {codigo}.";
        var enlace = new Uri($"https://wa.me/{numero[1..]}?text={Uri.EscapeDataString(mensaje)}");
#if ANDROID
        var actividad = Platform.CurrentActivity;
        if (actividad is not null)
        {
            foreach (var paquete in new[] { "com.whatsapp", "com.whatsapp.w4b" })
            {
                using var destino = Android.Net.Uri.Parse(enlace.AbsoluteUri);
                using var intent = new Android.Content.Intent(Android.Content.Intent.ActionView, destino);
                intent.SetPackage(paquete);
                try
                {
                    actividad.StartActivity(intent);
                    return;
                }
                catch (Android.Content.ActivityNotFoundException) { }
                catch (Java.Lang.SecurityException) { }
            }
        }
#else
        if (await Launcher.Default.TryOpenAsync(enlace))
            return;
#endif
        await DisplayAlertAsync("WhatsApp no disponible", "Puedes consultar el número o usar el marcador telefónico.", "Entendido");
    }

}
