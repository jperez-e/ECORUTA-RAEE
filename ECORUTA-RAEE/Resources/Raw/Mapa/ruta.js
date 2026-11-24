(() => {
  "use strict";
  const config = JSON.parse(document.getElementById("config").textContent);
  const estado = document.getElementById("estado");
  const mapa = L.map("mapa", {minZoom: 3, maxZoom: 19});
  const marcadores = new Map();
  const puntos = [];
  const credito = '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';
  const opcionesTramo = {color: "#218653", weight: 4, dashArray: "6 6", interactive: false};
  mapa.attributionControl.setPrefix(false);

  let anterior = null;
  for (const parada of config.paradas) {
    if (!Number.isFinite(parada.latitud) || !Number.isFinite(parada.longitud)) {
      anterior = null;
      continue;
    }

    const punto = [parada.latitud, parada.longitud];
    puntos.push(punto);
    // Una interrupción o una vista sin orden vigente nunca crea un atajo.
    if (anterior && Number.isInteger(parada.orden) && parada.orden === anterior.orden + 1)
      L.polyline([anterior.punto, punto], opcionesTramo).addTo(mapa);
    anterior = Number.isInteger(parada.orden) ? {punto, orden: parada.orden} : null;

    const distintivo = document.createElement("span");
    distintivo.textContent = Number.isInteger(parada.orden) ? String(parada.orden) : "•";
    const icono = L.divIcon({className:"parada", html: distintivo, iconSize:[32,32], iconAnchor:[16,16]});
    const detalle = document.createElement("div");
    detalle.className = "detalle";
    for (const texto of [parada.titulo, parada.direccion, parada.zona, parada.peso, parada.distancia]) {
      if (!texto) continue;
      const renglon = document.createElement("div");
      renglon.textContent = texto;
      detalle.appendChild(renglon);
    }
    const marcador = L.marker(punto, {icon:icono, title:parada.titulo, alt:parada.titulo})
      .addTo(mapa).bindPopup(detalle);
    marcadores.set(parada.id, marcador);
  }

  function mostrarTodas() {
    mapa.closePopup();
    if (puntos.length === 1) mapa.setView(puntos[0], 16);
    else mapa.fitBounds(puntos, {padding:[25,25], maxZoom:16});
  }

  function centrar(id) {
    const marcador = marcadores.get(id);
    if (!marcador) return;
    mapa.setView(marcador.getLatLng(), Math.max(mapa.getZoom(), 16));
    marcador.openPopup();
  }

  mostrarTodas();
  if (config.internet) {
    const capa = L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png",
      {noWrap:true, keepBuffer:0, attribution:credito});
    let cargadas = 0;
    capa.on("loading", () => { cargadas = 0; estado.textContent = "Cargando mapa base…"; });
    capa.on("tileload", () => { cargadas++; estado.textContent = ""; });
    capa.on("load", () => {
      estado.textContent = cargadas ? "" : "Mapa base no disponible. Consulta el listado de paradas.";
    });
    capa.addTo(mapa);
  } else {
    mapa.attributionControl.addAttribution(credito);
    estado.textContent = "Sin conexión. Se muestran las paradas sin mapa base.";
  }
  window.ecorutaRuta = Object.freeze({sesion:config.sesion, mostrarTodas, centrar});
})();
